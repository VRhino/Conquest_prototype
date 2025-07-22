using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Defines which entity the camera should follow and basic camera params.
/// </summary>
public struct CameraTargetComponent : IComponentData
{
    /// <summary>Entity to follow.</summary>
    public Entity followTarget;

    /// <summary>Base offset from the target.</summary>
    public float3 offset;

    /// <summary>Current zoom level of the camera.</summary>
    public float zoomLevel;

    /// <summary>Whether the tactical camera mode is enabled.</summary>
    public bool tacticalMode;
}
