using Unity.Entities;

/// <summary>
/// Component that holds the prefab reference for destination markers.
/// This should be added to a singleton entity to configure the marker prefab.
/// </summary>
public struct DestinationMarkerPrefabComponent : IComponentData
{
    /// <summary>
    /// The prefab entity to instantiate as destination markers.
    /// </summary>
    public Entity markerPrefab;
}
