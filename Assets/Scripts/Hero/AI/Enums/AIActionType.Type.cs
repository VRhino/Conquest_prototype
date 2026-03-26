/// <summary>
/// High-level action types output by AI decision systems.
/// Consumed by <see cref="HeroAIExecutionSystem"/> to produce movement and combat commands.
/// </summary>
public enum AIActionType
{
    /// <summary>No action — hero stays put.</summary>
    Idle,

    /// <summary>Move to a world position.</summary>
    MoveTo,

    /// <summary>Move toward a target entity and attack when in range.</summary>
    AttackTarget,

    /// <summary>Move into a zone and stay to capture it.</summary>
    CaptureZone,

    /// <summary>Move into a zone to contest an enemy capture.</summary>
    DefendZone,

    /// <summary>Move toward the hero's spawn point.</summary>
    Retreat
}
