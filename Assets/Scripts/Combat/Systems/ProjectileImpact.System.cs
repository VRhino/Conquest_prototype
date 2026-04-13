using Unity.Entities;

/// <summary>
/// Drains ProjectileImpactQueue each frame and creates PendingDamageEvent entities
/// so that DamageCalculationSystem processes projectile hits identically to melee hits
/// (FCT, shield block, kinetic bonus, height bonus, death — all unchanged).
/// Must run before DamageCalculationSystem.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(DamageCalculationSystem))]
public partial class ProjectileImpactSystem : SystemBase
{
    const int MaxImpactsPerFrame = 50;

    protected override void OnUpdate()
    {
        int processed = 0;

        while (processed < MaxImpactsPerFrame &&
               ProjectileImpactQueue.Pending.TryDequeue(out var data))
        {
            processed++;

            if (data.target == Unity.Entities.Entity.Null ||
                !SystemAPI.Exists(data.target) ||
                !SystemAPI.Exists(data.damageProfile))
                continue;

            if (SystemAPI.HasComponent<IsDeadComponent>(data.target))
                continue;

            var reqEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(reqEntity, new PendingDamageEvent
            {
                target           = data.target,
                damageSource     = data.shooter,
                damageProfile    = data.damageProfile,
                sourceTeam       = data.sourceTeam,
                category         = data.multiplier > 1f
                                       ? DamageCategory.Critical
                                       : DamageCategory.Normal,
                multiplier       = data.multiplier,
                attackerSpeed    = 0f,
                attackerPosition = data.attackerPosition,
                hitType          = data.hitType
            });
        }
    }
}
