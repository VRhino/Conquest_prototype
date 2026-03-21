using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Single source of truth for the squad's formation center position and rotation.
/// Written each frame by SquadAnchorSystem, consumed by FormationSystem,
/// GridFormationUpdateSystem, and DestinationMarkerSystem.
/// </summary>
public struct SquadFormationAnchorComponent : IComponentData
{
    public float3    position;
    public quaternion rotation;
}
