using Unity.Entities;

/// <summary>
/// Component placed on zone trigger entities.
/// Identifies the type and owner of the zone.
/// </summary>
public struct ZoneTriggerComponent : IComponentData
{
    /// <summary>Unique identifier of the zone.</summary>
    public int zoneId;

    /// <summary>Type of the zone.</summary>
    public ZoneType zoneType;

    /// <summary>Current team owner of the zone.</summary>
    public int teamOwner;

    /// <summary>Whether the zone is currently active.</summary>
    public bool isActive;

    /// <summary>Radius of the trigger used to detect entities.</summary>
    public float radius;

    /// <summary>True if the zone cannot be captured yet.</summary>
    public bool isLocked;

    /// <summary>Marks this zone as the final capture point.</summary>
    public bool isFinal;
}
