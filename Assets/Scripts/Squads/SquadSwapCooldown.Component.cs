using Unity.Entities;

/// <summary>
/// Placed on a hero entity after a successful squad swap.
/// Prevents another swap until <see cref="remainingTime"/> reaches zero.
/// </summary>
public struct SquadSwapCooldownComponent : IComponentData
{
    /// <summary>Seconds remaining before another swap is allowed.</summary>
    public float remainingTime;
}
