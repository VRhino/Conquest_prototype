using Unity.Entities;

/// <summary>
/// Event component requesting a squad swap for the hero.
/// Created when the player interacts with a supply point.
/// </summary>
public struct SquadSwapRequest : IComponentData
{
    /// <summary>Zone initiating the request.</summary>
    public int zoneId;
}
