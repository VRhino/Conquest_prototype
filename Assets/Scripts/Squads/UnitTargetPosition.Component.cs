using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Desired world position for a squad unit. Other systems should
/// move the unit towards this position.
/// </summary>
public struct UnitTargetPositionComponent : IComponentData
{
    public float3 position;
}
