using Unity.Entities;

/// <summary>
/// Buffer element that stores enemy entities currently targeted by a squad.
/// Populated by detection systems and consumed by <see cref="SquadAttackSystem"/>.
/// </summary>
public struct SquadTargetEntity : IBufferElementData
{
    /// <summary>Reference to the enemy entity.</summary>
    public Entity Value;
}
