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
    public int baseFuerza;
    public int baseDestreza;
    public int baseArmadura;
    public int baseVitalidad;

    [Header("Límites de Atributos")]
    public int minFuerza;
    public int maxFuerza;
    public int minDestreza;
    public int maxDestreza;
    public int minArmadura;
    public int maxArmadura;
    public int minVitalidad;
    public int maxVitalidad;

    /// <summary>Prefab of the weapon required for this class.</summary>
    public GameObject weaponPrefab;

    /// <summary>List of active abilities available to this class.</summary>
    public List<HeroAbilityData> abilities;

    /// <summary>Perks that can be chosen by heroes of this class.</summary>
    public List<PerkData> validClassPerks;
}
