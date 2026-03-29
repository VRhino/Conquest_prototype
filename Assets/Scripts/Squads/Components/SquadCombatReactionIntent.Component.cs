using Unity.Entities;

/// <summary>
/// Written by CombatReactionSystem each frame.
/// Signals that the squad has detected enemies or is under attack
/// and wants to react (stop moving, engage).
/// Read by OrderResolutionSystem with highest priority for remote squads.
/// </summary>
public struct SquadCombatReactionIntentComponent : IComponentData
{
    /// <summary>True = squad has active enemies in range or was hit this frame.</summary>
    public bool reactToEnemy;

    /// <summary>Primary enemy entity triggering the reaction.</summary>
    public Entity reactTarget;
}
