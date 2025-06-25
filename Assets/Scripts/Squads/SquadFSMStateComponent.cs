using Unity.Entities;

/// <summary>
/// Current finite state of a squad. Other systems use this value
/// to drive behaviour transitions.
/// </summary>
public struct SquadFSMStateComponent : IComponentData
{
    /// <summary>Current state of the squad FSM.</summary>
    public SquadFSMState currentState;
}

/// <summary>
/// Enumeration of possible squad states.
/// </summary>
public enum SquadFSMState
{
    Idle,
    FollowingHero,
    HoldingPosition,
    InCombat,
    Retreating,
    KO
}
