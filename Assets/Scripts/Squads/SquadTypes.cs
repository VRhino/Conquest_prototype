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
    Wedge,
    Column,
    Square
}

/// <summary>
/// Classification of squad unit types available to the player.
/// </summary>
public enum SquadType
{
    Squires,
    Archers,
    Pikemen,
    Lancers
}

/// <summary>
/// Tactical behavior profile used by AI systems.
/// </summary>
public enum BehaviorProfile
{
    Defensive,
    Harassing,
    AntiCharge,
    Versatile
}
