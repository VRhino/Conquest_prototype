using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Applies pending damage events using flat-reduction mitigation per damage type:
///
///   For each type T in {Blunt, Slashing, Piercing} where profile.T_damage > 0:
///     rawDmg    = profile.T_damage * multiplier
///     netDefense = max(T_defense - (profile.T_pen + unit.T_pen), 0)
///     contrib   = max(rawDmg - netDefense, rawDmg * 0.05)   ← floor 5% per type
///   D_eff = sum of all type contributions
///
/// Bonuses applied after mitigation:
///   Kinetic: +speed/maxSpeed * kineticMultiplier
///   Height:  +10% per metre of vertical advantage (> 0.5 m threshold)
///
/// The arithmetic is <see cref="DamageFormula.Compute"/> (pure, L1-03); this system gathers its inputs, applies shield
/// and health, and emits <see cref="DamageFeedbackEvent"/>s for the floating text instead of calling UI.
///
/// Shield check runs AFTER damage is calculated so the block absorbs the real
/// effective damage. If block goes to 0 the shield breaks, triggering a stun of
/// breakStunDuration seconds (from UnitShieldComponent). During stun the unit
/// cannot move and receives damage normally.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(SquadAISystem))]
public partial class DamageCalculationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var combatTuning = CombatTuning.Default;
        var defenseLookup     = GetComponentLookup<DefenseComponent>(true);
        var penetrationLookup = GetComponentLookup<PenetrationComponent>(true);
        var shieldLookup      = GetComponentLookup<UnitShieldComponent>();
        var transformLookup   = GetComponentLookup<LocalTransform>(true);
        var ecb               = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (pending, entity) in SystemAPI
                     .Query<RefRO<PendingDamageEvent>>()
                     .WithEntityAccess())
        {
            var p = pending.ValueRO;

            if (!SystemAPI.Exists(p.target) || (!p.hasInlineDamageProfile
                && !SystemAPI.HasComponent<DamageProfileComponent>(p.damageProfile)))
            {
                CompleteEvent(ref ecb, entity, p); continue;
            }

            if (SystemAPI.HasComponent<IsDeadComponent>(p.target))
            {
                CompleteEvent(ref ecb, entity, p); continue;
            }

            // Friendly fire check
            if (p.sourceTeam != Team.None &&
                SystemAPI.HasComponent<TeamComponent>(p.target))
            {
                if (SystemAPI.GetComponent<TeamComponent>(p.target).value == p.sourceTeam)
                {
                    CompleteEvent(ref ecb, entity, p); continue;
                }
            }

            // --- Calculate effectiveDmg (before shield so block absorbs real damage) ---
            var profile = p.hasInlineDamageProfile
                ? p.inlineDamageProfile
                : SystemAPI.GetComponent<DamageProfileComponent>(p.damageProfile);

            var def = defenseLookup.HasComponent(p.target)
                ? defenseLookup[p.target]
                : default;

            var pen = SystemAPI.Exists(p.damageSource) && penetrationLookup.HasComponent(p.damageSource)
                ? penetrationLookup[p.damageSource]
                : default;

            // Pure formula (L1-03): every input gathered here, no lookups inside.
            float kinetic = SystemAPI.Exists(p.damageSource) && SystemAPI.HasComponent<UnitWeaponComponent>(p.damageSource)
                ? SystemAPI.GetComponent<UnitWeaponComponent>(p.damageSource).kineticMultiplier : -1f;
            float heightDelta = transformLookup.HasComponent(p.target)
                ? p.attackerPosition.y - transformLookup[p.target].Position.y : 0f;
            float effectiveDmg = DamageFormula.Compute(new DamageInput
            {
                profile = profile, multiplier = p.multiplier, defense = def, penetration = pen,
                attackerSpeed = p.attackerSpeed, kineticMultiplier = kinetic, heightDelta = heightDelta,
            }, combatTuning).effective;

            // --- Shield block check (absorbs effectiveDmg when shield collider was hit) ---
            if (shieldLookup.HasComponent(p.target) && p.hitType == HitType.Shield)
            {
                var shield = shieldLookup[p.target];
                if (shield.currentBlock > 0f && shield.brokenTimer <= 0f)
                {
                    shield.currentBlock -= effectiveDmg;
                    if (shield.currentBlock <= 0f)
                    {
                        // Shield break — stun the unit
                        shield.currentBlock = 0f;
                        shield.brokenTimer  = shield.breakStunDuration;
                        if (transformLookup.HasComponent(p.target))
                        {
                            float3 bp = transformLookup[p.target].Position;
                            Feedback(ref ecb, bp, DamageCategory.ShieldBreak, 0f);
                        }
                    }
                    else
                    {
                        // Normal block — show absorbed damage amount
                        if (transformLookup.HasComponent(p.target))
                        {
                            float3 bp = transformLookup[p.target].Position;
                            Feedback(ref ecb, bp, DamageCategory.Blocked, effectiveDmg);
                        }
                    }
                    shieldLookup[p.target] = shield;
                    CompleteEvent(ref ecb, entity, p);
                    continue;
                }
            }

            // --- Apply to unit or hero HP ---
            bool willKill = false;
            if (SystemAPI.HasComponent<HealthComponent>(p.target))
            {
                var hp = SystemAPI.GetComponentRW<HealthComponent>(p.target);
                float prevHp = hp.ValueRO.currentHealth;
                hp.ValueRW.currentHealth = math.max(0f, prevHp - effectiveDmg);
                willKill = hp.ValueRO.currentHealth <= 0f;

                if (willKill && !SystemAPI.HasComponent<IsDeadComponent>(p.target))
                    ecb.AddComponent<IsDeadComponent>(p.target);

                // Signal the squad to retaliate (consumed by SquadAISystem this frame)
                if (SystemAPI.HasComponent<IsUnderAttackTag>(p.target))
                    ecb.SetComponentEnabled<IsUnderAttackTag>(p.target, true);

                // Body hit: knock the unit back, away from the attacker (consumed by UnitHitKnockbackSystem).
                if (!willKill && SystemAPI.HasComponent<UnitHitImpactComponent>(p.target)
                    && transformLookup.HasComponent(p.target))
                {
                    float2 away = transformLookup[p.target].Position.xz - p.attackerPosition.xz;
                    ecb.SetComponent(p.target, new UnitHitImpactComponent { direction = math.normalizesafe(away) });
                    ecb.SetComponentEnabled<UnitHitImpactComponent>(p.target, true);
                }
            }
            else if (SystemAPI.HasComponent<HeroHealthComponent>(p.target))
            {
                var hp = SystemAPI.GetComponentRW<HeroHealthComponent>(p.target);
                float prevHp = hp.ValueRO.currentHealth;
                hp.ValueRW.currentHealth = math.max(0f, prevHp - effectiveDmg);
                willKill = hp.ValueRO.currentHealth <= 0f;
                if (willKill && !SystemAPI.HasComponent<IsDeadComponent>(p.target))
                    ecb.AddComponent<IsDeadComponent>(p.target);
            }

            // FCT
            if (transformLookup.HasComponent(p.target))
            {
                float3 tp = transformLookup[p.target].Position;
                DamageCategory cat = willKill          ? DamageCategory.Death
                                   : p.multiplier > 1f ? DamageCategory.Critical
                                   :                     p.category;
                Feedback(ref ecb, tp, cat, effectiveDmg);
            }

            CompleteEvent(ref ecb, entity, p);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
    /// <summary>Floating text for the presentation layer (DamageFeedbackPresentationSystem).</summary>
    static void Feedback(ref EntityCommandBuffer ecb, float3 position, DamageCategory category, float amount)
    {
        Entity evt = ecb.CreateEntity();
        ecb.AddComponent(evt, new DamageFeedbackEvent { position = position, category = category, amount = amount });
    }

    static void CompleteEvent(ref EntityCommandBuffer ecb, Entity entity, PendingDamageEvent pending)
    {
        if (pending.destroyEntityAfterProcessing) ecb.DestroyEntity(entity);
        else ecb.RemoveComponent<PendingDamageEvent>(entity);
    }
}
