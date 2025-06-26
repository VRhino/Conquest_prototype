using Unity.Entities;

/// <summary>
/// Buffer element that stores references to zone entities.
/// </summary>
public struct ZoneReferenceBuffer : IBufferElementData
{
    /// <summary>Entity representing the zone.</summary>
    public Entity Value;
}
