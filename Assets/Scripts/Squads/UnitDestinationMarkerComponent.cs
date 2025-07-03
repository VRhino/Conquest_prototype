using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Component that stores information about a destination marker for a unit.
/// This marker shows where the unit is supposed to move to.
/// </summary>
public struct UnitDestinationMarkerComponent : IComponentData
{
    /// <summary>
    /// The entity of the marker prefab instance in the world.
    /// </summary>
    public Entity markerEntity;
    
    /// <summary>
    /// The target position where the unit should move to.
    /// </summary>
    public float3 targetPosition;
    
    /// <summary>
    /// Whether the marker is currently active/visible.
    /// </summary>
    public bool isActive;
    
    /// <summary>
    /// The unit that owns this marker.
    /// </summary>
    public Entity ownerUnit;
}
