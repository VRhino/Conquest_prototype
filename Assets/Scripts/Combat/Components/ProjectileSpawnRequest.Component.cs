using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// One-frame event entity created by RangedAttackSystem each time a unit fires.
/// ProjectileSpawnSystem reads these, spawns a pooled GO, then destroys the entity.
/// </summary>
public struct ProjectileSpawnRequest : IComponentData
{
    public Entity             shooter;

    public Entity             target;

    /// <summary>World position where the projectile spawns (unit torso height).</summary>
    public float3             spawnPosition;

    /// <summary>Normalized direction from spawn to target, with accuracy scatter applied.</summary>
    public float3             attackDirection;

    /// <summary>Entity carrying the DamageProfileComponent to apply on impact.</summary>
    public Entity             damageProfile;

    public Team               sourceTeam;

    /// <summary>1f for normal, criticalMultiplier for critical (rolled at fire time).</summary>
    public float              multiplier;

    /// <summary>ObjectPool key used to retrieve the correct prefab (e.g. "archer_arrow").</summary>
    public FixedString32Bytes poolKey;

    /// <summary>Arc = parabolic flight, Straight = flat line. Drives ProjectileController movement.</summary>
    public ProjectileTrajectory trajectory;
}
