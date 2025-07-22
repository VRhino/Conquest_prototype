using Unity.Entities;

/// <summary>
/// Event component generated when the player earns experience points.
/// </summary>
public struct XPEventComponent : IComponentData
{
    /// <summary>Amount of XP granted.</summary>
    public int amount;

    /// <summary>Reason the XP was awarded.</summary>
    public XPSource source;
}
