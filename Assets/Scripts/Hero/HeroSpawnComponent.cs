using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Stores the spawn location used when the hero respawns.
/// </summary>
public struct HeroSpawnComponent : IComponentData
{
    /// <summary>Position in world space for respawn.</summary>
    public float3 spawnPosition;

    /// <summary>Rotation applied on respawn.</summary>
    public quaternion spawnRotation;
}
