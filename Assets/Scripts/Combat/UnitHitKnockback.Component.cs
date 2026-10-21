using Unity.Entities;
using Unity.Mathematics;

/// <summary>Knock-back in progress. Written only by <see cref="UnitHitKnockbackSystem"/>.</summary>
public struct UnitHitKnockbackComponent : IComponentData
{
    public float2 direction;
    /// <summary>Seconds since the hit; at or above the duration the push is over.</summary>
    public float elapsed;
    public bool active;
}
