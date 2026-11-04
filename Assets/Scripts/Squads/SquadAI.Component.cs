using Unity.Entities;

/// <summary>
/// Component that tracks the current AI state of a squad.
/// </summary>
public struct SquadAIComponent : IComponentData
{
    /// <summary>Current AI state.</summary>
    public TacticalIntent  tacticalIntent;

    /// <summary>Current target entity if any.</summary>
    public Entity targetEntity;

    /// <summary>True if the squad is actively engaged in combat.</summary>
    public bool isInCombat;

    /// <summary>
    /// F12: at most ceil((1−q)·N) soldiers are away from their slot (<see cref="FormationLayout.IsFormationReady"/>).
    /// The AI hero waits for it before ordering a charge.
    /// </summary>
    public bool formationReady;

    /// <summary>Soldiers farther than the straggler distance from their resolved slot, isolated ones left out.</summary>
    public int stragglers;

    /// <summary>Soldiers whose slot has no free reachable cell (C13 <c>Unresolved</c>): they cannot arrive.</summary>
    public int isolated;
}
