using Unity.Entities;

/// <summary>
/// Event created when a capture zone is conquered.
/// </summary>
public struct ZoneCapturedEvent : IComponentData
{
    /// <summary>Identifier of the captured zone.</summary>
    public int zoneId;

    /// <summary>Team that completed the capture.</summary>
    public Team capturingTeam;
}
