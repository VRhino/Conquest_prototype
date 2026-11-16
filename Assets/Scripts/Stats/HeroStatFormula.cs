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
    public float bluntPenetrationPerStrength;
    public float slashingPenetrationPerStrength;
    public float slashingPenetrationPerDexterity;
    public float piercingPenetrationPerDexterity;
    /// <summary>Engine rules (<see cref="HeroEngineRules"/>): each type's damage is multiplied by 1 + these per point; 0 on asset classes.</summary>
    public float damageScalePerStrength;
    public float damageScalePerDexterity;
    /// <summary>Engine rules: relief of every damage type per point of armor; 0 on asset classes.</summary>
    public float reliefPerArmor;
}

/// <summary>
/// The class formulas of <see cref="HeroCalculatedAttributes.Calculate"/> for the stats the battle uses: health,
/// stamina, damage, defence and, since P3 (2026-11-08), the penetration that strength and dexterity give.
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
        bluntPenetrationPerStrength = c.bluntPenetrationMultiplierByStr,
        slashingPenetrationPerStrength = c.slashingPenetrationMultiplierByStr,
        slashingPenetrationPerDexterity = c.slashingPenetrationMultiplierByDex,
        piercingPenetrationPerDexterity = c.piercingPenetrationMultiplierByDex,
    };

    /// <summary>
    /// The formula of a ticket hero (<see cref="HeroEngineRules"/>): health from the rules' base and vitality, damage as the weapon's
    /// (the stack base) times 1 + a share per point of the class's damage attribute, relief from armor; no flat defense or penetration
    /// from the attributes. Stamina stays the class asset's.
    /// </summary>
    public static HeroStatFormulaComponent FromEngine(HeroClassDefinition c, in HeroEngineRules rules, in HeroEngineStatsComponent hero)
    {
        var f = new HeroStatFormulaComponent
        {
            baseHealth = rules.baseHealth,
            healthPerVitality = rules.healthPerVitality,
            damageScalePerStrength = hero.strengthDamageWeight * rules.damagePerAttribute,
            damageScalePerDexterity = hero.dexterityDamageWeight * rules.damagePerAttribute,
            reliefPerArmor = rules.reliefPerArmor,
        };
        if (c != null) (f.baseStamina, f.staminaPerDexterity) = (c.baseStamina, c.staminaPerDexterity);
        return f;
    }

    /// <summary>
    /// <paramref name="stat"/> of a hero before its modifiers: its stack base plus what the attributes give, and for damage, all of it
    /// times the engine rules' attribute scale.
    /// </summary>
    public static float Apply(in HeroStatFormulaComponent f, StatId stat, float baseValue,
        float strength, float dexterity, float armor, float vitality)
    {
        float value = baseValue + Derived(f, stat, strength, dexterity, armor, vitality);
        bool damage = stat == StatId.BluntDamage || stat == StatId.SlashDamage || stat == StatId.PierceDamage;
        return damage ? value * (1f + strength * f.damageScalePerStrength + dexterity * f.damageScalePerDexterity) : value;
    }

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
            case StatId.BluntPenetration: return strength * f.bluntPenetrationPerStrength;
            case StatId.SlashPenetration:
                return strength * f.slashingPenetrationPerStrength + dexterity * f.slashingPenetrationPerDexterity;
            case StatId.PiercePenetration: return dexterity * f.piercingPenetrationPerDexterity;
            case StatId.BluntRelief:
            case StatId.SlashRelief:
            case StatId.PierceRelief: return armor * f.reliefPerArmor;
            default: return 0f;
        }
    }
}
