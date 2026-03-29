using Unity.Entities;

/// <summary>
/// Holds the FSM state for a squad.
/// Single source of truth for squad FSM state — owned exclusively by SquadFSMSystem.
/// </summary>
public struct SquadFSMComponent : IComponentData
{
    /// <summary>Current tactical FSM state of the squad.</summary>
    public SquadFSMState currentState;

    /// <summary>Time elapsed in the current state.</summary>
    public float stateTimer;
}
