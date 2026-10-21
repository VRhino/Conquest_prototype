using Unity.Entities;

/// <summary>
/// Posture of a unit within its formation, chosen per slot role by <see cref="UnitPostureSystem"/>.
/// </summary>
public enum UnitStance
{
    /// <summary>Standard movement and combat posture.</summary>
    Normal        = 0,
    /// <summary>Shields locked in a wall, set and still.</summary>
    BracedShields = 1,
    /// <summary>Shield held in front (front rank of a shield formation).</summary>
    ShieldFront   = 2,
    /// <summary>Shield held to the side the unit faces (flanks of a testudo).</summary>
    ShieldSide    = 3,
    /// <summary>Shield over the head (interior of a testudo, against arrows).</summary>
    ShieldUp      = 4,
}

/// <summary>
/// Tracks the current posture of a unit.
/// Written only by UnitPostureSystem, from the formation's posture table and the unit's slot role.
/// Read by UnitAnimationSystem → UnitAnimationAdapter → Animator Controller.
/// </summary>
public struct UnitFormationStanceComponent : IComponentData
{
    public UnitStance stance;
}
