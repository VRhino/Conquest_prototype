using Unity.Entities;

/// <summary>
/// Class coefficients that turn a hero's attributes into its derived stats (Aprendizajes_Referencia.md T1, L7-3).
/// Copied from <see cref="HeroClassDefinition"/> when the hero is initialised, so <see cref="StatResolveSystem"/> can
/// derive the stats from the attributes resolved on the stack (base + equipment + perks) without managed data.
/// </summary>
public struct HeroStatFormulaComponent : IComponentData
{
    public float baseHealth;
    public float healthPerVitality;
    public float baseStamina;
    public float staminaPerDexterity;
    public float baseDamage;
    public float bluntDamagePerStrength;
    public float slashingDamagePerStrength;
    public float slashingDamagePerDexterity;
    public float piercingDamagePerDexterity;
    public float baseArmorValue;
    public float bluntDefensePerArmor;
    public float slashingDefensePerArmor;
    public float piercingDefensePerArmor;
}

/// <summary>
/// The class formulas of <see cref="HeroCalculatedAttributes.Calculate"/> for the stats the battle uses (health,
/// stamina, damage and defence). Penetration is left out on purpose: heroes have never had it in battle, and adding it
/// would change the balance.
/// </summary>
public static class HeroStatFormula
{
    public static HeroStatFormulaComponent From(HeroClassDefinition c) => new HeroStatFormulaComponent
    {
        baseHealth = c.baseHealth,
        healthPerVitality = c.healthPerVitality,
        baseStamina = c.baseStamina,
        staminaPerDexterity = c.staminaPerDexterity,
        baseDamage = c.baseDamage,
        bluntDamagePerStrength = c.bluntDamageMultiplierByStr,
        slashingDamagePerStrength = c.slashingDamageMultiplierByStr,
        slashingDamagePerDexterity = c.slashingDamageMultiplierByDex,
        piercingDamagePerDexterity = c.piercingDamageMultiplierByDex,
        baseArmorValue = c.baseArmorValue,
        bluntDefensePerArmor = c.bluntDefenseMultiplierByArmor,
        slashingDefensePerArmor = c.slashingDefenseMultiplierByArmor,
        piercingDefensePerArmor = c.piercingDefenseMultiplierByArmor,
    };

    /// <summary>Base that the attributes give to <paramref name="stat"/>; 0 for stats the formula does not cover.</summary>
    public static float Derived(in HeroStatFormulaComponent f, StatId stat,
        float strength, float dexterity, float armor, float vitality)
    {
        switch (stat)
        {
            case StatId.MaxHealth: return f.baseHealth + vitality * f.healthPerVitality;
            case StatId.MaxStamina: return f.baseStamina + dexterity * f.staminaPerDexterity;
            case StatId.BluntDamage: return f.baseDamage + strength * f.bluntDamagePerStrength;
            case StatId.SlashDamage:
                return f.baseDamage + strength * f.slashingDamagePerStrength + dexterity * f.slashingDamagePerDexterity;
            case StatId.PierceDamage: return f.baseDamage + dexterity * f.piercingDamagePerDexterity;
            case StatId.BluntDefense: return f.baseArmorValue + armor * f.bluntDefensePerArmor;
            case StatId.SlashDefense: return f.baseArmorValue + armor * f.slashingDefensePerArmor;
            case StatId.PierceDefense: return f.baseArmorValue + armor * f.piercingDefensePerArmor;
            default: return 0f;
        }
    }
}
