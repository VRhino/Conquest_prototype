using Unity.Entities;

/// <summary>
/// Combat parameters for a squad. Used by <see cref="SquadAttackSystem"/> to
/// execute abstract attacks at a fixed interval.
/// </summary>
public struct SquadCombatComponent : IComponentData
{
    /// <summary>Maximum distance at which squad attacks are effective.</summary>
    public float attackRange;

    /// <summary>Time in seconds between consecutive attack waves.</summary>
    public float attackInterval;

    /// <summary>Current timer accumulated since the last attack.</summary>
    public float attackTimer;
}
