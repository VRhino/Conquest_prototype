using Unity.Entities;

/// <summary>
/// Direction from which a unit's shield blocks incoming attacks.
/// </summary>
public enum ShieldOrientation : byte
{
    Forward = 0,
    Left    = 1,
    Right   = 2,
    All     = 3
}

/// <summary>
/// Shield state for units carrying shields (e.g. Spearmen front row).
/// Intercepts attacks from the configured orientation before the hurtbox is hit.
/// Regenerated over time by BlockRegenSystem.
/// </summary>
public struct UnitShieldComponent : IComponentData
{
    /// <summary>Current block value remaining.</summary>
    public float currentBlock;

    /// <summary>Maximum block capacity.</summary>
    public float maxBlock;

    /// <summary>Block points regenerated per second out of combat.</summary>
    public float regenRate;

    /// <summary>Direction the shield faces relative to the unit's forward.</summary>
    public ShieldOrientation orientation;
}
