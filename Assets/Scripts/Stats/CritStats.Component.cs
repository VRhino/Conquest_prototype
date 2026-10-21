using Unity.Entities;

/// <summary>
/// Critical stats resolved from the modifier stack (L1-04), written by <see cref="StatResolveSystem"/>. The attacker's
/// value and parameter give the rate; its critical-damage value, parameter and ratio, against the defender's
/// anti-critical, give the extra damage (<see cref="CritFormula"/>).
/// </summary>
public struct CritStatsComponent : IComponentData
{
    public float critValue;
    public float critParameter;
    public float critDamageValue;
    public float critDamageParameter;
    public float critDamageRatio;
    public float antiCritValue;
    public float antiCritParameter;
}
