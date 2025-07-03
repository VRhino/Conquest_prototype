using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

/// <summary>
/// Applies pending damage events to their targets using the damage profile data.
/// Critical hits use a multiplier for extra damage.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class DamageCalculationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var defenseLookup = GetComponentLookup<DefenseComponent>(true);
        var penetrationLookup = GetComponentLookup<PenetrationComponent>(true);
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (pending, entity) in SystemAPI
                     .Query<RefRO<PendingDamageEvent>>()
                     .WithEntityAccess())
        {
            if (!SystemAPI.Exists(pending.ValueRO.target) ||
                !SystemAPI.Exists(pending.ValueRO.damageProfile))
            {
                ecb.RemoveComponent<PendingDamageEvent>(entity);
                continue;
            }

            // Skip if target already dead
            if (SystemAPI.HasComponent<IsDeadComponent>(pending.ValueRO.target))
            {
                ecb.RemoveComponent<PendingDamageEvent>(entity);
                continue;
            }

            // Friendly fire check
            if (pending.ValueRO.sourceTeam != Team.None &&
                SystemAPI.HasComponent<TeamComponent>(pending.ValueRO.target))
            {
                var targetTeam = SystemAPI.GetComponent<TeamComponent>(pending.ValueRO.target).value;
                if (targetTeam == pending.ValueRO.sourceTeam)
                {
                    EntityManager.RemoveComponent<PendingDamageEvent>(entity);
                    continue;
                }
            }

            var profile = SystemAPI.GetComponent<DamageProfileComponent>(pending.ValueRO.damageProfile);

            float defense = 0f;
            if (defenseLookup.HasComponent(pending.ValueRO.target))
            {
                var def = defenseLookup[pending.ValueRO.target];
                defense = profile.damageType switch
                {
                    DamageType.Blunt => def.bluntDefense,
                    DamageType.Slashing => def.slashDefense,
                    DamageType.Piercing => def.pierceDefense,
                    _ => 0f
                };
            }

            float penetration = profile.penetration;
            if (SystemAPI.Exists(pending.ValueRO.damageSource) &&
                penetrationLookup.HasComponent(pending.ValueRO.damageSource))
            {
                var pen = penetrationLookup[pending.ValueRO.damageSource];
                penetration += profile.damageType switch
                {
                    DamageType.Blunt => pen.bluntPenetration,
                    DamageType.Slashing => pen.slashPenetration,
                    DamageType.Piercing => pen.piercePenetration,
                    _ => 0f
                };
            }

            float baseDamage = profile.baseDamage * pending.ValueRO.multiplier;
            float mitigatedDefense = math.max(0f, defense - penetration);
            float effectiveDamage = math.max(0f, baseDamage - mitigatedDefense);

            if (SystemAPI.HasComponent<HealthComponent>(pending.ValueRO.target))
            {
                var health = SystemAPI.GetComponentRW<HealthComponent>(pending.ValueRO.target);
                health.ValueRW.currentHealth = math.max(0f, health.ValueRO.currentHealth - effectiveDamage);

                if (health.ValueRW.currentHealth <= 0f &&
                    !SystemAPI.HasComponent<IsDeadComponent>(pending.ValueRO.target))
                {
                    ecb.AddComponent<IsDeadComponent>(pending.ValueRO.target);
                }
            }

            ecb.RemoveComponent<PendingDamageEvent>(entity);
        }
        
        // Execute all deferred changes
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
