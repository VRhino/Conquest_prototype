using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Defines a valid spawn location for heroes.
/// </summary>
public struct SpawnPointComponent : IComponentData
{
    /// <summary>Unique identifier for this spawn point.</summary>
    public int spawnID;

    /// <summary>Team that can use this spawn point.</summary>
    public int teamID;

    /// <summary>World position of the spawn point.</summary>
    public float3 position;

    /// <summary>Whether this spawn point is active.</summary>
    public bool isActive;
}
