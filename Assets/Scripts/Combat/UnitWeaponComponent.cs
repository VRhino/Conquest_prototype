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
}
