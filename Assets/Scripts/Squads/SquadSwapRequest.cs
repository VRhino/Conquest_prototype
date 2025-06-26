using Unity.Entities;

/// <summary>
/// Event component requesting a squad swap for the hero.
/// Created when the player selects a different squad while
/// interacting with a supply point.
/// </summary>
public struct SquadSwapRequest : IComponentData
{
    /// <summary>Identifier of the squad the player wants to activate.</summary>
    public int newSquadId;

    /// <summary>Zone that triggered the request.</summary>
    public int zoneId;
}
