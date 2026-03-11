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
}
