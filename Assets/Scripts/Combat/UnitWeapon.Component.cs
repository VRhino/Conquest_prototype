using Unity.Entities;

/// <summary>
/// Stores weapon parameters for a unit.
/// </summary>
public struct UnitWeaponComponent : IComponentData
{
    /// <summary>Entity holding the damage profile for this weapon.</summary>
    public Entity damageProfile;

    /// <summary>Maximum distance at which the attack is effective.</summary>
    public float attackRange;

    /// <summary>Time between consecutive attacks.</summary>
    public float attackInterval;

    /// <summary>
    /// Probability of landing a critical hit. Value between 0 and 1.
    /// Used by both units and the hero.
    /// </summary>
    public float criticalChance;
}
