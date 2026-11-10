using Unity.Entities;
using Unity.Mathematics;

/// <summary>Knock-back in progress. Written only by <see cref="UnitHitKnockbackSystem"/>.</summary>
public struct UnitHitKnockbackComponent : IComponentData
{
    public float2 direction;
    /// <summary>Seconds since the hit; at or above the duration the push is over.</summary>
    public float elapsed;
    public bool active;
    /// <summary>While the push lasts the unit walks through enemy bodies (heavy reactions, P2); from the hit.</summary>
    public bool passesEnemies;
    /// <summary>Length and duration of this push (the tuning's, or a heavy reaction's, L1-09).</summary>
    public float distance;
    public float duration;
}
