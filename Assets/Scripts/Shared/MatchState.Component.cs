using Unity.Entities;

/// <summary>
/// Component holding the current <see cref="MatchState"/> of the match
/// along with transition related data.
/// </summary>
public struct MatchStateComponent : IComponentData
{
    /// <summary>Current match state.</summary>
    public MatchState currentState;

    /// <summary>Timer used for countdowns in some states.</summary>
    public float stateTimer;

    /// <summary>Number of players that have confirmed readiness.</summary>
    public int playersReady;

    /// <summary>Maximum number of players in the match.</summary>
    public int maxPlayers;

    /// <summary>Flag set when victory conditions are achieved.</summary>
    public bool victoryConditionMet;
}
