using Unity.Entities;

/// <summary>
/// Event created when a supply zone changes ownership.
/// </summary>
public struct SupplyZoneCapturedEvent : IComponentData
{
    /// <summary>Identifier of the zone that was captured.</summary>
    public int zoneId;

    /// <summary>Team that obtained control of the zone.</summary>
    public Team capturingTeam;
}
