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
}

/// <summary>
/// Buffer element mapping abilities to the level they unlock every 10 levels.
/// </summary>
public struct AbilityByLevelElement : IBufferElementData
{
    public Entity Value;
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

