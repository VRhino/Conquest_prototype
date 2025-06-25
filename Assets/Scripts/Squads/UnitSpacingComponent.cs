using Unity.Entities;

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
}
