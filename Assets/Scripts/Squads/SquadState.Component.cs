using Unity.Entities;

/// <summary>
/// Stores the current state of a squad.
/// </summary>
public struct SquadStateComponent : IComponentData
{
    /// <summary>Formation currently being executed.</summary>
    public FormationType currentFormation;

    /// <summary>Current active order.</summary>
    public SquadOrderType currentOrder;

    /// <summary>True while the squad is executing an order.</summary>
    public bool isExecutingOrder;

    /// <summary>True if the squad is in combat.</summary>
    public bool isInCombat;

    /// <summary>Time remaining before another formation can be selected.</summary>
    public float formationChangeCooldown;

    /// <summary>Current tactical FSM state.</summary>
    public SquadFSMState currentState;

    /// <summary>Desired state to transition into.</summary>
    public SquadFSMState transitionTo;

    /// <summary>Timer for the current state.</summary>
    public float stateTimer;

    /// <summary>True if the controlling hero is still alive.</summary>
    public bool lastOwnerAlive;

    /// <summary>Set when a retreat has been triggered.</summary>
    public bool retreatTriggered;
}
