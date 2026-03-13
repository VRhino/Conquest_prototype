using Unity.Entities;

/// <summary>
/// Tag placed on a hero entity to trigger the actual squad swap execution
/// after channeling completes.
/// </summary>
public struct SquadSwapExecuteTag : IComponentData
{
    /// <summary>Int ID of the new squad to spawn.</summary>
    public int newSquadId;
}
