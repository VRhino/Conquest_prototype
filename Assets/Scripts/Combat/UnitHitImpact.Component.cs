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
}
