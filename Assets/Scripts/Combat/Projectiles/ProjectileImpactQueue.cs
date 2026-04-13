using System.Collections.Concurrent;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Thread-safe static bridge for projectile hits from MonoBehaviour back to ECS.
/// ProjectileController enqueues impact data; ProjectileImpactSystem drains it each frame.
/// </summary>
public static class ProjectileImpactQueue
{
    public static readonly ConcurrentQueue<ProjectileImpactData> Pending = new();
}

/// <summary>
/// All data needed to construct a PendingDamageEvent from a projectile hit.
/// </summary>
public struct ProjectileImpactData
{
    public Entity shooter;
    public Entity target;
    public Entity damageProfile;
    public Team   sourceTeam;

    /// <summary>World position where the projectile was fired from (for height bonus).</summary>
    public float3 attackerPosition;

    /// <summary>1f for normal hit, criticalMultiplier for critical (determined at fire time).</summary>
    public float  multiplier;

    /// <summary>Whether impact was on shield collider (Shield) or body (Body).</summary>
    public HitType hitType;
}
