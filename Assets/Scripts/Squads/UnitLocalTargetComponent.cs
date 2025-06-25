using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Desired local position of a unit inside its squad formation. Other
/// systems move the unit towards this target.
/// </summary>
public struct UnitLocalTargetComponent : IComponentData
{
    /// <summary>Local position that the unit should aim to occupy.</summary>
    public float3 targetPosition;
}
