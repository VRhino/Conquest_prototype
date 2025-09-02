using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// Defines the parameters for a hero class. Used by initialization and attribute systems.
/// </summary>
[CreateAssetMenu(menuName = "Hero/Class Definition")]
public class HeroClassDefinition : ScriptableObject
{
    /// <summary>Type of hero class this definition represents.</summary>
    public HeroClass heroClass;

    /// <summary>Icon shown in selection menus.</summary>
    public Sprite icon;

    /// <summary>Text description of the class role.</summary>
    public string description;

    [Header("Atributos Base")]
    public int baseStrength;
    public int baseDexterity;
    public int baseArmor;
    public int baseVitality;

    [Header("Constantes de Cálculo")]
    public float baseHealth = 100f;
    public float baseStamina = 100f;
    public float baseDamage = 5f;
    public float baseArmorValue = 0f;

    [Header("Variables de cálculo Basico")]
    public float healthPerVitality = 5f;
    public float staminaPerDexterity = 1.5f;

    [Header("Variables de cálculo Avanzado(Damage)")]
    public float bluntDamageMultiplierByStr = 3f;
    public float piercingDamageMultiplierByDex = 2f;
    public float slashingDamageMultiplierByStr = 1.3f;
    public float slashingDamageMultiplierByDex = 1f;

    [Header("Variables de cálculo Avanzado(Penetration)")]
    public float bluntPenetrationMultiplierByStr = 1f;
    public float piercingPenetrationMultiplierByDex = 1f;
    public float slashingPenetrationMultiplierByStr = 1f;
    public float slashingPenetrationMultiplierByDex = 1f;

    [Header("Variables de cálculo Avanzado(Defense)")]

    public float bluntDefenseMultiplierByArmor = 0.5f;
    public float piercingDefenseMultiplierByArmor = 0.5f;
    public float slashingDefenseMultiplierByArmor = 0.5f;
    public float blockPowerMultiplierByStr = 0.5f;
    public float movementSpeedBase = 5f;
    public float movementSpeedDexMultiplier = 0.1f;

    /// <summary>List of active abilities available to this class.</summary>
    public List<HeroAbility> abilities;

    /// <summary>Perks that can be chosen by heroes of this class.</summary>
    public List<HeroPerk> validClassPerks;
}
