using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Runtime representation of <see cref="SquadData"/> used by squad systems.
/// </summary>
public struct SquadDataComponent : IComponentData
{
    public float baseHealth;
    public float baseSpeed;
    public float mass;
    public float weight;
    public float block;
    public float blockRegenRate;
    public float shieldBreakStunDuration;

    public float slashingDefense;
    public float piercingDefense;
    public float bluntDefense;

    public float slashingDamage;
    public float piercingDamage;
    public float bluntDamage;

    public float slashingPenetration;
    public float piercingPenetration;
    public float bluntPenetration;

    public bool isRangedUnit;
    public float range;
    public float accuracy;
    public float fireRate;
    public float reloadSpeed;
    public int ammoCapacity;
    public FixedString32Bytes projectilePoolKey;
    public ProjectileTrajectory projectileTrajectory;
    /// <summary>Projectiles per shot and the fan they open into (L2-7).</summary>
    public int projectilesPerShot;
    public float fanAngleDegrees;

    /// <summary>Progression curves sampled per level.</summary>
    public BlobAssetReference<SquadProgressionCurveBlob> curves;

    public float attackRange;
    public float attackInterval;
    public float criticalChance;
    public float criticalMultiplier;

    // Strike window timing
    public float strikeWindowStart;
    public float strikeWindowDuration;
    public float attackAnimationDuration;

    // Kinetic
    public float kineticMultiplier;

    /// <summary>The weapon's hit traits (melee if any, else ranged), copied into the squad's damage profile.</summary>
    public HitTraits hitTraits;

    /// <summary>Body radius of each soldier (L5-5); 0 = the global one.</summary>
    public float bodyRadius;

    // Squad stamina (L5-7); a maximum of 0 = none.
    public float staminaMax;
    public float staminaRunCostPerSoldier;
    public float staminaRecoveryPerSecond;
    public float staminaRecoverFraction;
}

/// <summary>
/// Buffer element mapping abilities to the level they unlock every 10 levels.
/// </summary>
public struct AbilityByLevelElement : IBufferElementData
{
    public Entity Value;
}

/// <summary>A conditional damage modifier the squad type declares (L1-02); each unit gets it on spawn.</summary>
public struct SquadConditionalDamageElement : IBufferElementData
{
    public ConditionalDamage value;
}

/// <summary>A baked squad ability (L2-9): its name, cooldown and the skill the squad launches, in its slot.</summary>
public struct SquadAbilityDefinitionComponent : IComponentData
{
    public FixedString64Bytes name;
    public float cooldown;
    public SkillSlot slot;
    public BlobAssetReference<SkillBlob> skill;
}

/// <summary>
/// Component used on squad entities to reference their <see cref="SquadDataComponent"/> entity.
/// </summary>
public struct SquadDataReference : IComponentData
{
    public Entity dataEntity;
}

/// <summary>
/// Stores the string ID of a squad data entity so it can be looked up at runtime by ID.
/// </summary>
public struct SquadDataIDComponent : IComponentData
{
    public FixedString64Bytes id;
}

