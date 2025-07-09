using Unity.Entities;

/// <summary>
/// Component holding runtime data for a weapon damage profile.
/// Typically baked from a <see cref="DamageProfile"/> ScriptableObject.
/// </summary>
public struct DamageProfileComponent : IComponentData
{
    /// <summary>Base damage dealt by the attack.</summary>
    public float baseDamage;

    /// <summary>Type of damage inflicted.</summary>
    public DamageType damageType;

    /// <summary>Penetration value for the damage.</summary>
    public float penetration;
}
