using Unity.Entities;

/// <summary>
/// Holds the current <see cref="GamePhase"/> of the game.
/// </summary>
public struct GameStateComponent : IComponentData
{
    /// <summary>
    /// Phase the game is currently in.
    /// </summary>
    public GamePhase currentPhase;
}
