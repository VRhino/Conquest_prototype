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

    [Header("Límites de Atributos")]
    public int minStrength;
    public int maxStrength;
    public int minDexterity;
    public int maxDexterity;
    public int minArmor;
    public int maxArmor;
    public int minVitality;
    public int maxVitality;

    /// <summary>List of active abilities available to this class.</summary>
    public List<HeroAbilityData> abilities;

    /// <summary>Perks that can be chosen by heroes of this class.</summary>
    public List<PerkData> validClassPerks;
}
