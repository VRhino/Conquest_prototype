using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Assigned formation slot for a unit. The <see cref="relativeOffset"/> is
/// relative to the squad leader and indicates where the unit should be
/// positioned inside the formation.
/// </summary>
public struct UnitFormationSlotComponent : IComponentData
{
    /// <summary>Offset from the leader in local squad space.</summary>
    public float3 relativeOffset;

    /// <summary>Index of the slot within the formation pattern.</summary>
    public int slotIndex;
}
