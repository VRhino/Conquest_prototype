using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Enableable event: a hit reached this unit's body this frame. Enabled by <see cref="DamageCalculationSystem"/>,
/// consumed by <see cref="UnitHitKnockbackSystem"/>, which starts the knock-back away from the attacker.
/// </summary>
public struct UnitHitImpactComponent : IComponentData, IEnableableComponent
{
    /// <summary>Horizontal direction from the attacker to the unit (the push direction), normalised.</summary>
    public float2 direction;
    /// <summary>A heavy hit (knock-down, launch): the pushed unit goes through enemies while it lasts (P2). No hit sets it yet.</summary>
    public bool passesEnemies;
}
