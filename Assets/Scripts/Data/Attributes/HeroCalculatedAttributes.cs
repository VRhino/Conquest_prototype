using System;

/// <summary>
/// Estructura que contiene los atributos finales calculados del héroe.
/// Combina stats base, bonificaciones de equipamiento y modificaciones temporales.
/// Esta estructura reemplaza a CalculatedAttributes con una separación más clara de responsabilidades.
/// </summary>
[Serializable]
public struct HeroCalculatedAttributes
{
    #region Primary Attributes
    
    /// <summary>Final strength value (base + equipment + temporary).</summary>
    public float finalStrength;
    
    /// <summary>Final dexterity value (base + equipment + temporary).</summary>
    public float finalDexterity;
    
    /// <summary>Final armor value (base + equipment + temporary).</summary>
    public float finalArmor;
    
    /// <summary>Final vitality value (base + equipment + temporary).</summary>
    public float finalVitality;

    #endregion

    #region Derived Attributes

    /// <summary>Maximum health points.</summary>
    public float maxHealth;
    
    /// <summary>Maximum stamina points.</summary>
    public float stamina;

    /// <summary>Blunt damage capability.</summary>
    public float bluntDamage;
    
    /// <summary>Slashing damage capability.</summary>
    public float slashingDamage;
    
    /// <summary>Piercing damage capability.</summary>
    public float piercingDamage;

    /// <summary>Blunt damage defense.</summary>
    public float bluntDefense;
    
    /// <summary>Slashing damage defense.</summary>
    public float slashDefense;
    
    /// <summary>Piercing damage defense.</summary>
    public float pierceDefense;

    /// <summary>Blunt penetration capability.</summary>
    public float bluntPenetration;
    
    /// <summary>Slashing penetration capability.</summary>
    public float slashPenetration;
    
    /// <summary>Piercing penetration capability.</summary>
    public float piercePenetration;

    /// <summary>Block power for defensive actions.</summary>
    public float blockPower;
    
    /// <summary>Movement speed in units per second.</summary>
    public float movementSpeed;
    
    /// <summary>Leadership points for squad management.</summary>
    public float leadership;

    #endregion

    #region Component Sources (for debugging and UI)

    /// <summary>Base stats component used in calculation.</summary>
    public HeroBaseStats baseStats;
    
    /// <summary>Equipment bonuses component used in calculation.</summary>
    public EquipmentBonuses equipmentBonuses;
    
    /// <summary>Temporary modifications component used in calculation.</summary>
    public EquipmentBonuses temporaryModifications;

    #endregion

    /// <summary>
    /// Creates an empty calculated attributes structure.
    /// </summary>
    public static HeroCalculatedAttributes Empty => new HeroCalculatedAttributes();

    /// <summary>
    /// Calculates final attributes from base stats, equipment bonuses, temporary modifications and class definition.
    /// </summary>
    /// <param name="baseStats">Base hero statistics</param>
    /// <param name="equipmentBonuses">Bonuses from equipped items</param>
    /// <param name="temporaryMods">Temporary modifications (usually from UI)</param>
    /// <param name="classDefinition">Class-specific calculation constants</param>
    /// <returns>Fully calculated attributes</returns>
    public static HeroCalculatedAttributes Calculate(
        HeroBaseStats baseStats, 
        EquipmentBonuses equipmentBonuses, 
        EquipmentBonuses temporaryMods, 
        HeroClassDefinition classDefinition)
    {
        if (classDefinition == null)
        {
            return Empty;
        }

        var result = new HeroCalculatedAttributes
        {
            baseStats = baseStats,
            equipmentBonuses = equipmentBonuses,
            temporaryModifications = temporaryMods
        };

        // Calculate final primary attributes
        result.finalStrength = baseStats.baseStrength + equipmentBonuses.strengthBonus + temporaryMods.strengthBonus;
        result.finalDexterity = baseStats.baseDexterity + equipmentBonuses.dexterityBonus + temporaryMods.dexterityBonus;
        result.finalArmor = baseStats.baseArmor + equipmentBonuses.armorBonus + temporaryMods.armorBonus;
        result.finalVitality = baseStats.baseVitality + equipmentBonuses.vitalityBonus + temporaryMods.vitalityBonus;

        // Calculate derived attributes using class-specific formulas
        result.maxHealth = classDefinition.baseHealth + (result.finalVitality * classDefinition.healthPerVitality);
        result.stamina = classDefinition.baseStamina + (result.finalDexterity * classDefinition.staminaPerDexterity);

        // Damage calculations
        result.bluntDamage = classDefinition.baseDamage + (result.finalStrength * classDefinition.bluntDamageMultiplierByStr);
        result.slashingDamage = classDefinition.baseDamage + 
            (result.finalStrength * classDefinition.slashingDamageMultiplierByStr) +
            (result.finalDexterity * classDefinition.slashingDamageMultiplierByDex);
        result.piercingDamage = classDefinition.baseDamage + (result.finalDexterity * classDefinition.piercingDamageMultiplierByDex);

        // Defense calculations
        result.bluntDefense = classDefinition.baseArmorValue + (result.finalArmor * classDefinition.bluntDefenseMultiplierByArmor);
        result.slashDefense = classDefinition.baseArmorValue + (result.finalArmor * classDefinition.slashingDefenseMultiplierByArmor);
        result.pierceDefense = classDefinition.baseArmorValue + (result.finalArmor * classDefinition.piercingDefenseMultiplierByArmor);

        // Penetration calculations
        result.bluntPenetration = result.finalStrength * classDefinition.bluntPenetrationMultiplierByStr;
        result.slashPenetration = 
            (result.finalStrength * classDefinition.slashingPenetrationMultiplierByStr) +
            (result.finalDexterity * classDefinition.slashingPenetrationMultiplierByDex);
        result.piercePenetration = result.finalDexterity * classDefinition.piercingPenetrationMultiplierByDex;

        // Other calculations
        result.blockPower = result.finalStrength * classDefinition.blockPowerMultiplierByStr;
        result.movementSpeed = classDefinition.movementSpeedBase + (result.finalDexterity * classDefinition.movementSpeedDexMultiplier);
        result.leadership = 700f; // Default leadership value

        return result;
    }
}
