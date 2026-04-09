using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Cached world position and rotation of the hero that owns this squad.
/// Updated every frame by HeroPositionCacheSystem.
/// Eliminates Squad→Hero lookups in formation and movement systems.
/// </summary>
public struct HeroWorldPositionComponent : IComponentData
{
    public float3     position;
    public quaternion rotation;
}
