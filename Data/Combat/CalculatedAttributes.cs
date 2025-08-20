using System;

/// <summary>
/// Cached combat statistics derived from the hero's base attributes, equipment
/// and active perks. Used by combat systems to avoid recalculating complex
/// formulas every frame.
/// </summary>
[Serializable]
public class CalculatedAttributes
{
    /// <summary>Total health after combining Vitality, class bonuses and gear.</summary>
    public float maxHealth;

    /// <summary>Stamina pool for actions and abilities derived from gear and perks.</summary>
    public float stamina;

    /// <summary>Effective Strength including equipment and perks.</summary>
    public float strength;

    /// <summary>Effective Dexterity including equipment and perks.</summary>
    public float dexterity;

    /// <summary>Effective Vitality used for health calculations.</summary>
    public float vitality;

    /// <summary>Total armor rating from base stat, class and worn items.</summary>
    public float armor;

    /// <summary>Final blunt damage dealt after applying all modifiers.</summary>
    public float bluntDamage;

    /// <summary>Final slashing damage dealt after applying all modifiers.</summary>
    public float slashingDamage;

    /// <summary>Final piercing damage dealt after applying all modifiers.</summary>
    public float piercingDamage;

    /// <summary>Defense value against blunt attacks.</summary>
    public float bluntDefense;

    /// <summary>Defense value against slashing attacks.</summary>
    public float slashDefense;

    /// <summary>Defense value against piercing attacks.</summary>
    public float pierceDefense;

    /// <summary>Penetration applied to enemy blunt defense.</summary>
    public float bluntPenetration;

    /// <summary>Penetration applied to enemy slash defense.</summary>
    public float slashPenetration;

    /// <summary>Penetration applied to enemy pierce defense.</summary>
    public float piercePenetration;

    /// <summary>Power of blocks with shields or weapons.</summary>
    public float blockPower;

    /// <summary>Movement speed in units per second after modifiers.</summary>
    public float movementSpeed;

    /// <summary>Leadership capacity for squad management (base 700 + equipment stats).</summary>
    public float leadership;
}

