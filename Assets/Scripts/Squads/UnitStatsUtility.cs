using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Centralized utility for applying scaled stats to squad units.
/// Avoids logic duplication between SquadProgressionSystem and UnitStatScalingSystem.
/// </summary>
public static class UnitStatsUtility
{
    /// <summary>
    /// Applies level-scaled stats to all units in a squad.
    /// </summary>
    public static void ApplyStatsToSquad(
        Entity squadEntity,
        SquadDataComponent data,
        int leadershipCost,
        int level,
        EntityManager entityManager,
        BufferLookup<SquadUnitElement> unitBufferLookup)
    {
        if (!unitBufferLookup.HasBuffer(squadEntity))
            return;

        // Calculate multipliers based on level
        int index = math.clamp(level - 1, 0, data.curves.Value.health.Length - 1);
        float healthMul = data.curves.Value.health[index];
        float damageMul = data.curves.Value.damage[index];
        float defenseMul = data.curves.Value.defense[index];
        float speedMul = data.curves.Value.speed[index];

        // Apply stats to each unit
        DynamicBuffer<SquadUnitElement> units = unitBufferLookup[squadEntity];
        foreach (var unitElement in units)
        {
            if (!entityManager.Exists(unitElement.Value))
                continue;

            ApplyStatsToUnit(unitElement.Value, data, leadershipCost, healthMul, damageMul, defenseMul, speedMul, entityManager);
        }
    }

    /// <summary>
    /// Applies scaled stats to an individual unit.
    /// </summary>
    public static void ApplyStatsToUnit(
        Entity unitEntity,
        SquadDataComponent data,
        int leadershipCost,
        float healthMul,
        float damageMul,
        float defenseMul,
        float speedMul,
        EntityManager entityManager)
    {
        // Calculate final speed using centralized utility
        int weightCategory = (int)math.round(data.weight);
        float finalSpeed = UnitSpeedCalculator.CalculateFinalSpeed(data.baseSpeed, speedMul, weightCategory);

        // Create main stats component
        var stats = new UnitStatsComponent
        {
            health = data.baseHealth * healthMul,
            speed = finalSpeed,
            mass = data.mass,
            weight = weightCategory,
            block = data.block,
            slashingDefense = data.slashingDefense * defenseMul,
            piercingDefense = data.piercingDefense * defenseMul,
            bluntDefense = data.bluntDefense * defenseMul,
            slashingDamage = data.slashingDamage * damageMul,
            piercingDamage = data.piercingDamage * damageMul,
            bluntDamage = data.bluntDamage * damageMul,
            slashingPenetration = data.slashingPenetration,
            piercingPenetration = data.piercingPenetration,
            bluntPenetration = data.bluntPenetration,
            leadershipCost = leadershipCost
        };

        // Apply main stats
        if (entityManager.HasComponent<UnitStatsComponent>(unitEntity))
            entityManager.SetComponentData(unitEntity, stats);
        else
            entityManager.AddComponentData(unitEntity, stats);

        // Apply ranged stats if applicable
        if (data.isRangedUnit)
        {
            var rangedStats = new UnitRangedStatsComponent
            {
                range             = data.range,
                accuracy          = data.accuracy,
                fireRate          = data.fireRate,
                reloadSpeed       = data.reloadSpeed,
                totalAmmo         = data.ammoCapacity,
                projectilePoolKey = data.projectilePoolKey,
                trajectory        = data.projectileTrajectory
            };

            if (entityManager.HasComponent<UnitRangedStatsComponent>(unitEntity))
                entityManager.SetComponentData(unitEntity, rangedStats);
            else
                entityManager.AddComponentData(unitEntity, rangedStats);

            // Initialize ranged attack state (ammo full, not reloading)
            var attackState = new RangedAttackStateComponent
            {
                isReloading = false,
                reloadTimer = 0f,
                currentAmmo = data.ammoCapacity,
                shotTimer   = 0f
            };

            if (entityManager.HasComponent<RangedAttackStateComponent>(unitEntity))
                entityManager.SetComponentData(unitEntity, attackState);
            else
                entityManager.AddComponentData(unitEntity, attackState);
        }
        else if (entityManager.HasComponent<UnitRangedStatsComponent>(unitEntity))
        {
            entityManager.RemoveComponent<UnitRangedStatsComponent>(unitEntity);
            if (entityManager.HasComponent<RangedAttackStateComponent>(unitEntity))
                entityManager.RemoveComponent<RangedAttackStateComponent>(unitEntity);
        }
    }
}
