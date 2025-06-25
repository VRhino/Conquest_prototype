using Unity.Entities;

/// <summary>
/// Enumerates the possible orders that can be issued to a squad.
/// </summary>
public enum SquadOrderType
{
    FollowHero,
    HoldPosition,
    Attack
}

/// <summary>
/// Supported squad formations.
/// </summary>
public enum FormationType
{
    Line,
    Dispersed,
    Testudo,
    Wedge
}
