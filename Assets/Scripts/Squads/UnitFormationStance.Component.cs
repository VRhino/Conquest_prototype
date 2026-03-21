using Unity.Entities;

/// <summary>
/// Tactical stance of a unit within its formation.
/// Normal    – standard movement and combat posture.
/// BracedShields – Shield Wall formation; unit locks shields, applies mass bonus, acts as NavMesh wall.
/// </summary>
public enum UnitStance
{
    Normal        = 0,
    BracedShields = 1,
}

/// <summary>
/// Tracks the current tactical stance of a unit.
/// Written by FormationStanceSystem in response to milestone tags.
/// Read by UnitAnimationSystem → UnitAnimationAdapter → Animator Controller.
/// </summary>
public struct UnitFormationStanceComponent : IComponentData
{
    public UnitStance stance;
}
