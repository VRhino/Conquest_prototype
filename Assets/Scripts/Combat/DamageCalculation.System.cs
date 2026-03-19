using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Applies pending damage events using ratio-based mitigation:
///
///   ratio      = defense / max(penetration, 0.001)
///   mitigation = min(ratio, 0.95)                  ← floor 5% damage always lands
///   D_eff      = baseDamage * multiplier * (1 - mitigation)
///
/// Bonuses applied after mitigation:
///   Kinetic: +speed/maxSpeed * kineticMultiplier
///   Height:  +10% per metre of vertical advantage (> 0.5 m threshold)
///
/// Shield check runs before damage: blocks frontal hits when currentBlock > 0.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
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
            { ecb.RemoveComponent<PendingDamageEvent>(entity); continue; }

            if (SystemAPI.HasComponent<IsDeadComponent>(p.target))
            { ecb.RemoveComponent<PendingDamageEvent>(entity); continue; }

            // Friendly fire check
            if (p.sourceTeam != Team.None &&
                SystemAPI.HasComponent<TeamComponent>(p.target))
            {
                if (SystemAPI.GetComponent<TeamComponent>(p.target).value == p.sourceTeam)
                { ecb.RemoveComponent<PendingDamageEvent>(entity); continue; }
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
                    continue;
                }
            }

            // Resolve defense and penetration by damage type
            var profile = SystemAPI.GetComponent<DamageProfileComponent>(p.damageProfile);

            float defense = 0f;
            if (defenseLookup.HasComponent(p.target))
            {
                var def = defenseLookup[p.target];
                defense = profile.damageType switch
                {
                    DamageType.Blunt    => def.bluntDefense,
                    DamageType.Slashing => def.slashDefense,
                    DamageType.Piercing => def.pierceDefense,
                    _                   => 0f
                };
            }

            float penetration = profile.penetration;
            if (SystemAPI.Exists(p.damageSource) &&
                penetrationLookup.HasComponent(p.damageSource))
            {
                var pen = penetrationLookup[p.damageSource];
                penetration += profile.damageType switch
                {
                    DamageType.Blunt    => pen.bluntPenetration,
                    DamageType.Slashing => pen.slashPenetration,
                    DamageType.Piercing => pen.piercePenetration,
                    _                   => 0f
                };
            }

            // Ratio-based mitigation with 5% damage floor
            float ratio        = defense / math.max(penetration, 0.001f);
            float mitigation   = math.min(ratio, 0.95f);
            float effectiveDmg = profile.baseDamage * p.multiplier * (1f - mitigation);

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

            // Apply to unit or hero
            if (SystemAPI.HasComponent<HealthComponent>(p.target))
            {
                var hp = SystemAPI.GetComponentRW<HealthComponent>(p.target);
                hp.ValueRW.currentHealth = math.max(0f, hp.ValueRO.currentHealth - effectiveDmg);
                if (hp.ValueRO.currentHealth <= 0f &&
                    !SystemAPI.HasComponent<IsDeadComponent>(p.target))
                    ecb.AddComponent<IsDeadComponent>(p.target);
            }
            else if (SystemAPI.HasComponent<HeroHealthComponent>(p.target))
            {
                var hp = SystemAPI.GetComponentRW<HeroHealthComponent>(p.target);
                hp.ValueRW.currentHealth = math.max(0f, hp.ValueRO.currentHealth - effectiveDmg);
                if (hp.ValueRO.currentHealth <= 0f &&
                    !SystemAPI.HasComponent<IsDeadComponent>(p.target))
                    ecb.AddComponent<IsDeadComponent>(p.target);
            }

            ecb.RemoveComponent<PendingDamageEvent>(entity);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
