using Unity.Entities;

/// <summary>
/// Optional component used to unlock zones in sequence.
/// </summary>
public struct ZoneLinkComponent : IComponentData
{
    /// <summary>Zone that must be captured to unlock this one.</summary>
    public int requiredZoneId;
}
