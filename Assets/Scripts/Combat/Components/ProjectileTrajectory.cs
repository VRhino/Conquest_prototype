/// <summary>
/// Defines how a projectile travels from shooter to target.
/// Assigned per squad type via SquadData and flows through the ECS pipeline
/// into ProjectileController.Initialize() where it selects the movement logic.
/// </summary>
public enum ProjectileTrajectory : byte
{
    /// <summary>Parabolic arc — for archers. Height scales with horizontal distance.</summary>
    Arc      = 0,

    /// <summary>Straight line at constant speed — for crossbowmen and siege weapons.</summary>
    Straight = 1,
}
