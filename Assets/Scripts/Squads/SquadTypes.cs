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

/// <summary>
/// Classification of squad unit types available to the player.
/// </summary>
public enum SquadType
{
    Escuderos,
    Arqueros,
    Piqueros,
    Lanceros
}

/// <summary>
/// Tactical behavior profile used by AI systems.
/// </summary>
public enum BehaviorProfile
{
    Defensivo,
    Hostigador,
    Anticarga,
    Versatil
}
