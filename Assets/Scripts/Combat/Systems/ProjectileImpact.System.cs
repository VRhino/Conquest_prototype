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
    private EntityArchetype _damageEventArchetype;

    protected override void OnCreate()
        => _damageEventArchetype = EntityManager.CreateArchetype(typeof(PendingDamageEvent));

    protected override void OnUpdate()
    {
        int processed = 0;
        uint tick = CombatRandom.Tick(SystemAPI.Time.ElapsedTime);

        while (processed < MaxImpactsPerFrame &&
               ProjectileImpactQueue.Pending.TryDequeue(out var data))
        {
            processed++;

            if (data.target == Unity.Entities.Entity.Null ||
                !SystemAPI.Exists(data.target))
                continue;

            if (SystemAPI.HasComponent<IsDeadComponent>(data.target))
                continue;

            var reqEntity = EntityManager.CreateEntity(_damageEventArchetype);
            EntityManager.SetComponentData(reqEntity, new PendingDamageEvent
            {
                destroyEntityAfterProcessing = true,
                target           = data.target,
                damageSource     = data.shooter,
                damageProfile    = data.damageProfile,
                hasInlineDamageProfile = true,
                inlineDamageProfile = data.damageProfileSnapshot,
                sourceTeam       = data.sourceTeam,
                category         = data.multiplier > 1f
                                       ? DamageCategory.Critical
                                       : DamageCategory.Normal,
                multiplier       = data.multiplier,
                attackerSpeed    = 0f,
                attackerPosition = data.attackerPosition,
                hitType          = data.hitType,
                // Rolled when the hit is built, like the reference (L1-12).
                varianceOffset   = CombatRandom.RollVariance(tick, data.shooter, data.target,
                                       data.damageProfileSnapshot.traits.damageVariance),
                hitForm          = HitForm.Ranged,
            });
        }
    }
}
