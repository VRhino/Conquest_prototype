using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Defines an entity to be represented on the minimap.
/// </summary>
public struct MinimapIconComponent : IComponentData
{
    /// <summary>Visual icon type.</summary>
    public MinimapIconType iconType;

    /// <summary>Team affiliation used for color coding.</summary>
    public Team teamAffiliation;

    /// <summary>Current world position of the entity.</summary>
    public float3 worldPosition;
}
