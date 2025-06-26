using Unity.Entities;

/// <summary>
/// Classification of functional zones within a map.
/// </summary>
public enum ZoneType
{
    /// <summary>Zone that can be captured by teams.</summary>
    Capture,
    /// <summary>Zone that supplies or heals units.</summary>
    Supply,
    /// <summary>Spawn location for heroes or squads.</summary>
    Spawn
}
