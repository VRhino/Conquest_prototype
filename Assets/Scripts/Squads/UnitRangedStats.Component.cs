using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Additional stats for ranged units such as archers.
/// Only added to entities when the squad data specifies it is a ranged unit.
/// </summary>
public struct UnitRangedStatsComponent : IComponentData
{
    public float range;
    public float accuracy;
    public float fireRate;
    public float reloadSpeed;
    public int totalAmmo;

    /// <summary>ObjectPool key used to spawn the correct projectile prefab (e.g. "archer_arrow").</summary>
    public FixedString32Bytes projectilePoolKey;

    /// <summary>How the projectile travels: Arc (archers) or Straight (crossbowmen).</summary>
    public ProjectileTrajectory trajectory;
}
