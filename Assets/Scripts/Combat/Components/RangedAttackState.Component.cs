using Unity.Entities;

/// <summary>
/// Per-unit state for ranged attack cycle: reload tracking and shot timing.
/// Added alongside UnitRangedStatsComponent by UnitStatsUtility when isRangedUnit is true.
/// </summary>
public struct RangedAttackStateComponent : IComponentData
{
    /// <summary>True while the unit is reloading (ammo depleted).</summary>
    public bool  isReloading;

    /// <summary>Seconds remaining in the current reload.</summary>
    public float reloadTimer;

    /// <summary>Remaining shots before reload is required.</summary>
    public int   currentAmmo;

    /// <summary>Seconds remaining until the next shot is allowed (1 / fireRate cooldown).</summary>
    public float shotTimer;

    /// <summary>
    /// True for exactly one ECS frame when a projectile is fired.
    /// UnitAnimationAdapter reads this to trigger the Shoot animation.
    /// RangedAttackSystem clears it at the start of each OnUpdate.
    /// </summary>
    public bool isFiring;
}
