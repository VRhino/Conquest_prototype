using Unity.Entities;

/// <summary>
/// Event emitted when the match state changes. Systems can listen
/// for this component to react to state transitions.
/// </summary>
public struct GameStateChangeEvent : IComponentData
{
    /// <summary>The new state that became active.</summary>
    public MatchState newState;
}
