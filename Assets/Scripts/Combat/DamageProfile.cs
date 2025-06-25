using UnityEngine;

/// <summary>
/// Defines base damage, penetration and type for a weapon.
/// </summary>
[CreateAssetMenu(menuName = "Combat/Damage Profile")]
public class DamageProfile : ScriptableObject
{
    /// <summary>Base damage dealt by the attack.</summary>
    public float baseDamage;

    /// <summary>Type of damage inflicted.</summary>
    public DamageType damageType;

    /// <summary>Penetration value for the damage.</summary>
    public float penetration;
}
