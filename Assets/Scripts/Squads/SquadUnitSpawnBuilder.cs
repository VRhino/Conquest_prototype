using ConquestTactics.Animation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>Immutable inputs shared by units created in one squad spawn.</summary>
public struct SquadUnitSpawnContext
{
    public Entity squad;
    public Entity hero;
    public Entity damageProfile;
    public SquadDataComponent data;
    public SquadSpawnConfigComponent config;
    public SquadType squadType;
    public int instanceId;
    public float speed;
    public quaternion rotation;
}

/// <summary>Composes a runtime unit from squad data without choosing its formation slot.</summary>
public static class SquadUnitSpawnBuilder
{
    public static void Configure(EntityCommandBuffer ecb, Entity unit,
        SquadUnitSpawnContext context, in UnitGridSlotComponent slot, float3 worldPosition)
    {
        var data = context.data;
        ecb.AddComponent(unit, new UnitOwnerComponent
        { squad = context.squad, hero = context.hero });
#if UNITY_EDITOR
        ecb.SetName(unit, $"Unit_{slot.slotIndex}_{context.squadType}_Sq{context.instanceId}");
#endif
        ecb.AddComponent(unit, new LocalTransform
        {
            Position = worldPosition,
            Rotation = context.rotation,
            Scale = 1f
        });
        ecb.AddComponent(unit, new UnitVisualReference
        { visualPrefabName = GetUnitVisualPrefabName(context.squadType) });
        ecb.AddComponent(unit, new UnitEquipmentComponent
        {
            armorPercent = 100f,
            hasDebuff = false,
            isDeployable = true
        });

        ecb.AddComponent<UnitCombatComponent>(unit);
        ecb.AddComponent(unit, new MeleeContactSlotComponent { target = Entity.Null, slot = -1 });
        ecb.AddComponent(unit, new UnitTargetPositionComponent { position = worldPosition });
        ecb.AddComponent(unit, new UnitFormationStateComponent
        { State = UnitFormationState.Formed });

        ecb.AddComponent<UnitStartedMovingTag>(unit);
        ecb.SetComponentEnabled<UnitStartedMovingTag>(unit, false);
        ecb.AddComponent<UnitArrivedAtSlotTag>(unit);
        ecb.SetComponentEnabled<UnitArrivedAtSlotTag>(unit, false);
        ecb.AddComponent<IsEngagingTag>(unit);
        ecb.SetComponentEnabled<IsEngagingTag>(unit, false);
        ecb.AddComponent<IsUnderAttackTag>(unit);
        ecb.SetComponentEnabled<IsUnderAttackTag>(unit, false);

        ecb.AddComponent(unit, new UnitFormationStanceComponent { stance = UnitStance.Normal });
        ecb.AddComponent(unit, slot);
        ecb.AddComponent(unit, new UnitOrientationComponent
        {
            orientationType = UnitOrientationType.MatchHeroDirection,
            rotationSpeed = context.config.unitRotationSpeed
        });
        ecb.AddComponent(unit, new UnitAnimationMovementComponent
        {
            CurrentSpeed = 0f,
            MaxSpeed = context.speed,
            MovementDirection = new float3(0, 0, 1),
            IsMoving = false,
            IsRunning = false,
            MovementTime = 0f,
            StoppedTime = 0f,
            PreviousPosition = worldPosition,
            CurrentStance = UnitStance.Normal,
            SlotRow = slot.row,
            SlotRole = (int)slot.role
        });
        ecb.AddComponent(unit, new UnitStatsComponent
        {
            health = data.baseHealth,
            speed = context.speed,
            mass = data.mass,
            weight = data.weight,
            block = data.block,
            slashingDefense = data.slashingDefense,
            piercingDefense = data.piercingDefense,
            bluntDefense = data.bluntDefense,
            slashingDamage = data.slashingDamage,
            piercingDamage = data.piercingDamage,
            bluntDamage = data.bluntDamage,
            slashingPenetration = data.slashingPenetration,
            piercingPenetration = data.piercingPenetration,
            bluntPenetration = data.bluntPenetration
        });
        ecb.AddComponent(unit, new HealthComponent
        { maxHealth = data.baseHealth, currentHealth = data.baseHealth });
        ecb.AddComponent(unit, new DefenseComponent
        {
            bluntDefense = data.bluntDefense,
            slashDefense = data.slashingDefense,
            pierceDefense = data.piercingDefense
        });
        ecb.AddComponent(unit, new PenetrationComponent
        {
            // Base penetration lives on the weapon profile; these are additive bonuses.
            bluntPenetration = 0f,
            slashPenetration = 0f,
            piercePenetration = 0f
        });
        ecb.AddComponent(unit, new UnitWeaponComponent
        {
            damageProfile = context.damageProfile,
            attackRange = data.attackRange,
            attackInterval = data.attackInterval,
            criticalChance = data.criticalChance,
            criticalMultiplier = data.criticalMultiplier,
            strikeWindowStart = data.strikeWindowStart,
            strikeWindowDuration = data.strikeWindowDuration,
            attackAnimationDuration = data.attackAnimationDuration,
            kineticMultiplier = data.kineticMultiplier
        });
        ecb.AddComponent<UnitRotationIntentComponent>(unit);
        ecb.AddComponent<UnitMovementGearComponent>(unit);
        ecb.AddComponent<WeaponHitboxActiveTag>(unit);
        ecb.SetComponentEnabled<WeaponHitboxActiveTag>(unit, false);

        if (data.isRangedUnit)
        {
            ecb.AddComponent(unit, new UnitRangedStatsComponent
            {
                range = data.range,
                accuracy = data.accuracy,
                fireRate = data.fireRate,
                reloadSpeed = data.reloadSpeed,
                totalAmmo = data.ammoCapacity,
                projectilePoolKey = data.projectilePoolKey,
                trajectory = data.projectileTrajectory
            });
            ecb.AddComponent(unit, new RangedAttackStateComponent
            {
                isReloading = false,
                reloadTimer = 0f,
                currentAmmo = data.ammoCapacity,
                shotTimer = 0f
            });
        }
    }

    private static FixedString64Bytes GetUnitVisualPrefabName(SquadType squadType)
    {
        return squadType switch
        {
            SquadType.Squires => "UnitVisual_Escudero",
            SquadType.Archers => "UnitVisual_Arquero",
            SquadType.Pikemen => "UnitVisual_Pikemen",
            SquadType.Spearmen => "UnitVisual_Spearmen",
            _ => "UnitVisual_Default"
        };
    }
}
