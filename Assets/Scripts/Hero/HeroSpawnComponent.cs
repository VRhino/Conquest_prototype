using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
/// <summary>
/// Stores the spawn location used when the hero respawns.
/// Also tracks the selected spawn point.
/// </summary>
public struct HeroSpawnComponent : IComponentData
{
    /// <summary>Identifier of the chosen spawn point.</summary>
    public int spawnId;

    /// <summary>Position in world space for respawn.</summary>
    public float3 spawnPosition;

    /// <summary>Rotation applied on respawn.</summary>
    public quaternion spawnRotation;

    /// <summary>True when the hero has been placed at the spawn.</summary>
    public bool hasSpawned;

    /// <summary>
    /// Unique identifier for the visual prefab used for this hero.
    public FixedString64Bytes visualPrefabId;
}
