using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Parameters that control how much space a unit maintains from its
/// allies inside the squad.
/// </summary>
public struct UnitSpacingComponent : IComponentData
{
    /// <summary>Minimum desired distance to other units.</summary>
    public float minDistance;

    /// <summary>Strength of the repelling force applied when overlapping.</summary>
    public float repelForce;
    /// <summary>Slot asignado en la grid (x, y)</summary>
    public int2 Slot;
}
