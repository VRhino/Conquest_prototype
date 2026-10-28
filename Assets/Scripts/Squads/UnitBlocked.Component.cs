using Unity.Entities;

/// <summary>
/// The enemy body that stops a moving unit and is not its target (M2/M16, P4): the nearest enemy within ±30° of where the
/// unit wants to go that <see cref="EnemyContact"/> clips it against. Written only by <see cref="UnitAvoidanceSystem"/>;
/// <see cref="UnitTargetingSystem"/> takes the blocker as the target once the block lasts
/// <see cref="MovementTuningComponent.blockedRetargetTime"/>.
/// </summary>
public struct UnitBlockedComponent : IComponentData
{
    /// <summary>Entity.Null when nothing blocks the unit.</summary>
    public Entity blocker;
    /// <summary>Elapsed time when this blocker started blocking.</summary>
    public double since;
}
