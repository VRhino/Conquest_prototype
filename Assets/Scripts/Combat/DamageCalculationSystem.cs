using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Applies pending damage events to their targets using the damage profile data.
/// Critical hits use a multiplier for extra damage.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class DamageCalculationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach (var (pending, entity) in SystemAPI
                     .Query<RefRO<PendingDamageEvent>>()
                     .WithEntityAccess())
        {
            if (!SystemAPI.Exists(pending.ValueRO.target) ||
                !SystemAPI.Exists(pending.ValueRO.damageProfile))
            {
                SystemAPI.RemoveComponent<PendingDamageEvent>(entity);
                continue;
            }

            var profile = SystemAPI.GetComponent<DamageProfileData>(pending.ValueRO.damageProfile);
            float finalDamage = profile.baseDamage * pending.ValueRO.multiplier;

            if (SystemAPI.HasComponent<HealthComponent>(pending.ValueRO.target))
            {
                var health = SystemAPI.GetComponentRW<HealthComponent>(pending.ValueRO.target);
                health.ValueRW.currentHealth = math.max(0f, health.ValueRO.currentHealth - finalDamage);
            }

            SystemAPI.RemoveComponent<PendingDamageEvent>(entity);
        }
    }
}
