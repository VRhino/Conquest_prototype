using Unity.Entities;

/// <summary>
/// Current movement gear of a unit. Written only by UnitFollowFormationSystem; read by
/// metrics and, later, animation and network snapshots.
/// </summary>
public struct UnitMovementGearComponent : IComponentData
{
    public MovementGear gear;
}
