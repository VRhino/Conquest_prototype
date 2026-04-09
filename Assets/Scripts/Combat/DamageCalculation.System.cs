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
/// Shield check runs before damage: blocks frontal hits when currentBlock > 0.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(SquadAISystem))]
public partial class DamageCalculationSystem : SystemBase
{
    protected override void OnUpdate()
    {
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



            if (!SystemAPI.Exists(p.target) || !SystemAPI.Exists(p.damageProfile))
            {
                bool tMissing = !SystemAPI.Exists(p.target);
                bool pMissing = !SystemAPI.Exists(p.damageProfile);

                ecb.RemoveComponent<PendingDamageEvent>(entity); continue;
            }

            if (SystemAPI.HasComponent<IsDeadComponent>(p.target))
            {

                ecb.RemoveComponent<PendingDamageEvent>(entity); continue;
            }

            // Friendly fire check
            if (p.sourceTeam != Team.None &&
                SystemAPI.HasComponent<TeamComponent>(p.target))
            {
                if (SystemAPI.GetComponent<TeamComponent>(p.target).value == p.sourceTeam)
                {

                    ecb.RemoveComponent<PendingDamageEvent>(entity); continue;
                }
            }

            // Shield block check — intercepts frontal attacks before hurtbox
            if (shieldLookup.HasComponent(p.target) && shieldLookup[p.target].currentBlock > 0f)
            {
                var shield  = shieldLookup[p.target];
                bool blocked = false;
                if (transformLookup.HasComponent(p.target))
                {
                    float3 targetFwd = math.forward(transformLookup[p.target].Rotation);
                    float  dot       = math.dot(math.normalizesafe(p.attackDirection), -targetFwd);
                    blocked = shield.orientation switch
                    {
                        ShieldOrientation.Forward => dot > 0.5f,
                        ShieldOrientation.All     => true,
                        _                         => false
                    };
                }
                if (blocked)
                {

                    shield.currentBlock     = math.max(0f, shield.currentBlock - 50f);
                    shieldLookup[p.target]  = shield;
                    ecb.RemoveComponent<PendingDamageEvent>(entity);

                    if (transformLookup.HasComponent(p.target))
                    {
                        float3 bp = transformLookup[p.target].Position;
                        FloatingCombatTextManager.Instance?.Spawn(
                            new UnityEngine.Vector3(bp.x, bp.y + 1.8f, bp.z),
                            DamageCategory.Blocked, 0f);
                    }
                    continue;
                }
            }

            // Resolve defense and penetration per type — sum contributions of all non-zero damage types
            var profile = SystemAPI.GetComponent<DamageProfileComponent>(p.damageProfile);

            var def = defenseLookup.HasComponent(p.target)
                ? defenseLookup[p.target]
                : default;

            var pen = SystemAPI.Exists(p.damageSource) && penetrationLookup.HasComponent(p.damageSource)
                ? penetrationLookup[p.damageSource]
                : default;

            float effectiveDmg = 0f;

            if (profile.bluntDamage > 0f)
            {
                float raw    = profile.bluntDamage * p.multiplier;
                float netDef = math.max(def.bluntDefense - (profile.bluntPenetration + pen.bluntPenetration), 0f);
                effectiveDmg += math.max(raw - netDef, raw * 0.05f);
            }
            if (profile.slashingDamage > 0f)
            {
                float raw    = profile.slashingDamage * p.multiplier;
                float netDef = math.max(def.slashDefense - (profile.slashingPenetration + pen.slashPenetration), 0f);
                effectiveDmg += math.max(raw - netDef, raw * 0.05f);
            }
            if (profile.piercingDamage > 0f)
            {
                float raw    = profile.piercingDamage * p.multiplier;
                float netDef = math.max(def.pierceDefense - (profile.piercingPenetration + pen.piercePenetration), 0f);
                effectiveDmg += math.max(raw - netDef, raw * 0.05f);
            }

            // Kinetic bonus — attacker speed increases penetration effectiveness
            if (p.attackerSpeed > 0f)
            {
                float km = 0.3f;
                if (SystemAPI.Exists(p.damageSource) &&
                    SystemAPI.HasComponent<UnitWeaponComponent>(p.damageSource))
                    km = SystemAPI.GetComponent<UnitWeaponComponent>(p.damageSource).kineticMultiplier;

                const float kMaxSpeed   = 5f; // reference speed for normalization
                float kineticBonus = 1f + (p.attackerSpeed / kMaxSpeed) * km;
                effectiveDmg *= kineticBonus;
            }

            // Height bonus — ~10% per metre of vertical advantage (threshold 0.5 m)
            if (transformLookup.HasComponent(p.target))
            {
                float heightDelta = p.attackerPosition.y - transformLookup[p.target].Position.y;
                if (heightDelta > 0.5f)
                    effectiveDmg *= 1f + heightDelta * 0.1f;
            }

            effectiveDmg = math.max(0f, effectiveDmg);



            if (FloatingCombatTextManager.Instance == null)


            const float FctYOffset = 1.8f;

            // Apply to unit or hero — determinar willKill del resultado (una sola lectura de HP)
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

            // Emitir FCT con categoría correcta
            if (transformLookup.HasComponent(p.target))
            {
                float3 tp = transformLookup[p.target].Position;
                DamageCategory cat = willKill          ? DamageCategory.Death
                                   : p.multiplier > 1f ? DamageCategory.Critical
                                   :                     p.category;
                FloatingCombatTextManager.Instance?.Spawn(
                    new UnityEngine.Vector3(tp.x, tp.y + FctYOffset, tp.z),
                    cat, effectiveDmg);
            }

            ecb.RemoveComponent<PendingDamageEvent>(entity);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
