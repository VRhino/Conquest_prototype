using Unity.Entities;

/// <summary>
/// Tracks whether a squad is in normal or brace combat mode.
/// Set by BraceWeaponActivationSystem when the squad enters HoldPosition.
/// </summary>
public enum SquadCombatMode : byte
{
    Normal = 0,
    Brace  = 1
}

/// <summary>
/// Current combat mode of a squad entity.
/// </summary>
public struct SquadCombatModeComponent : IComponentData
{
    public SquadCombatMode mode;
}

/// <summary>
/// Per-row weapon parameter override applied to UnitWeaponComponent during Brace mode.
/// Stored as a DynamicBuffer on the squad entity.
/// Row 0 = front rank. BraceWeaponSystem applies these overrides each frame.
/// </summary>
public struct BraceRowProfile : IBufferElementData
{
    /// <summary>Formation row index this profile applies to (0 = front rank).</summary>
    public int row;

    /// <summary>Distance from unit origin to far edge of damage box (= attackRange).</summary>
    public float attackRange;

    /// <summary>Seconds from animation start when hitbox activates.</summary>
    public float strikeWindowStart;

    /// <summary>Duration the hitbox stays active.</summary>
    public float strikeWindowDuration;

    /// <summary>Total duration of the attack animation.</summary>
    public float attackAnimationDuration;
}
