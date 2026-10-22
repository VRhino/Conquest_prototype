using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Centralized utility for applying scaled stats to squad units.
/// Avoids logic duplication between SquadProgressionSystem and UnitStatScalingSystem.
/// </summary>
public static class UnitStatsUtility
{
    static readonly StatId[] DefenseStats = { StatId.BluntDefense, StatId.SlashDefense, StatId.PierceDefense };
    static readonly StatId[] DamageStats = { StatId.BluntDamage, StatId.SlashDamage, StatId.PierceDamage };

    /// <summary>Base damage and penetration of the squad profile, with the level ratio on damage.</summary>
    static void SeedSquadProfile(EntityManager em, Entity profile, SquadDataComponent data, float damageMul)
    {
        StatStack.Ensure(em, profile);
        StatStack.SetBase(em, profile, StatId.BluntDamage, data.bluntDamage);
        StatStack.SetBase(em, profile, StatId.SlashDamage, data.slashingDamage);
        StatStack.SetBase(em, profile, StatId.PierceDamage, data.piercingDamage);
        StatStack.SetBase(em, profile, StatId.BluntPenetration, data.bluntPenetration);
        StatStack.SetBase(em, profile, StatId.SlashPenetration, data.slashingPenetration);
        StatStack.SetBase(em, profile, StatId.PiercePenetration, data.piercingPenetration);
        StatStack.ClearSource(em, profile, ModifierSource.Level);
        foreach (var stat in DamageStats) StatStack.AddLevelMultiplier(em, profile, stat, damageMul);
        StatStack.MarkDirty(em, profile);
    }

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
        float healthMul = 1f, damageMul = 1f, defenseMul = 1f, speedMul = 1f;
        if (data.curves.IsCreated)
        {
            ref var curves = ref data.curves.Value;
            healthMul = SquadProgressionCurves.Read(ref curves.health, level);
            damageMul = SquadProgressionCurves.Read(ref curves.damage, level);
            defenseMul = SquadProgressionCurves.Read(ref curves.defense, level);
            speedMul = SquadProgressionCurves.Read(ref curves.speed, level);
        }

        // Apply stats to each unit
        // Applying a missing component may invalidate DynamicBuffer handles.
        using var units = unitBufferLookup[squadEntity].ToNativeArray(Allocator.Temp);
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
        // Level-1 speed: the weight category is part of the base; the level curve is a modifier.
        int weightCategory = (int)math.round(data.weight);
        float baseSpeed = UnitSpeedCalculator.CalculateFinalSpeed(data.baseSpeed, 1f, weightCategory);

        // Identity fields (not stats) stay with the spawner and this utility.
        if (entityManager.HasComponent<UnitStatsComponent>(unitEntity))
        {
            var identity = entityManager.GetComponentData<UnitStatsComponent>(unitEntity);
            identity.mass = data.mass;
            identity.weight = weightCategory;
            identity.block = data.block;
            identity.leadershipCost = leadershipCost;
            identity.slashingPenetration = data.slashingPenetration;
            identity.piercingPenetration = data.piercingPenetration;
            identity.bluntPenetration = data.bluntPenetration;
            entityManager.SetComponentData(unitEntity, identity);
        }
        else
            entityManager.AddComponentData(unitEntity, new UnitStatsComponent
            {
                mass = data.mass, weight = weightCategory, block = data.block, leadershipCost = leadershipCost,
                slashingPenetration = data.slashingPenetration, piercingPenetration = data.piercingPenetration,
                bluntPenetration = data.bluntPenetration,
            });

        // Stats go through the modifier stack (T1): squad data is the base, the level curves are Level ratios.
        // StatResolveSystem writes health (keeping its fraction), defense and UnitStatsComponent.
        StatStack.Ensure(entityManager, unitEntity);
        StatStack.SetBase(entityManager, unitEntity, StatId.MaxHealth, data.baseHealth);
        StatStack.SetBase(entityManager, unitEntity, StatId.MoveSpeed, baseSpeed);
        StatStack.SetBase(entityManager, unitEntity, StatId.BluntDefense, data.bluntDefense);
        StatStack.SetBase(entityManager, unitEntity, StatId.SlashDefense, data.slashingDefense);
        StatStack.SetBase(entityManager, unitEntity, StatId.PierceDefense, data.piercingDefense);
        StatStack.SetBase(entityManager, unitEntity, StatId.BluntDamage, data.bluntDamage);
        StatStack.SetBase(entityManager, unitEntity, StatId.SlashDamage, data.slashingDamage);
        StatStack.SetBase(entityManager, unitEntity, StatId.PierceDamage, data.piercingDamage);
        // Weapon penetration lives on the squad profile; the unit's own is an additive bonus (0 by default).
        StatStack.ClearSource(entityManager, unitEntity, ModifierSource.Level);
        StatStack.AddLevelMultiplier(entityManager, unitEntity, StatId.MaxHealth, healthMul);
        StatStack.AddLevelMultiplier(entityManager, unitEntity, StatId.MoveSpeed, speedMul);
        foreach (var stat in DefenseStats) StatStack.AddLevelMultiplier(entityManager, unitEntity, stat, defenseMul);
        foreach (var stat in DamageStats) StatStack.AddLevelMultiplier(entityManager, unitEntity, stat, damageMul);
        StatStack.SeedCritical(entityManager, unitEntity, data.criticalChance, data.criticalMultiplier);
        StatStack.MarkDirty(entityManager, unitEntity);

        // The squad's shared damage profile carries its own stack: base damage and penetration, level ratio on damage.
        if (entityManager.HasComponent<UnitWeaponComponent>(unitEntity))
        {
            var profileEntity = entityManager.GetComponentData<UnitWeaponComponent>(unitEntity).damageProfile;
            if (entityManager.Exists(profileEntity) && entityManager.HasComponent<DamageProfileComponent>(profileEntity))
                SeedSquadProfile(entityManager, profileEntity, data, damageMul);
        }

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
                trajectory        = data.projectileTrajectory,
                projectilesPerShot = data.projectilesPerShot,
                fanAngleDegrees   = data.fanAngleDegrees
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

            // Recalculation must not refill ammo or interrupt a reload.
            if (!entityManager.HasComponent<RangedAttackStateComponent>(unitEntity))
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
