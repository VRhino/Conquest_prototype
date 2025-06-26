using Unity.Entities;

/// <summary>
/// Request component created when the player selects a spawn point in the UI.
/// </summary>
public struct SpawnSelectionRequest : IComponentData
{
    /// <summary>ID of the spawn point selected by the player.</summary>
    public int spawnId;
}
