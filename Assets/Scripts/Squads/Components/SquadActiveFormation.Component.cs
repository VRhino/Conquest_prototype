using Unity.Entities;

/// <summary>
/// Tracks the currently active formation for a squad.
/// Single source of truth for formation — owned exclusively by Formation systems.
/// </summary>
public struct SquadActiveFormationComponent : IComponentData
{
    /// <summary>Formation currently being executed by this squad.</summary>
    public FormationType currentFormation;

    /// <summary>Cooldown before another formation change can be requested.</summary>
    public float formationChangeCooldown;
}
