using Unity.Entities;

/// <summary>
/// Shared candidate list for every unit in a squad.
/// Populated by detection and consumed by targeting, AI and debug views.
/// </summary>
public struct SquadTargetEntity : IBufferElementData
{
    /// <summary>Reference to the enemy entity.</summary>
    public Entity Value;
}
