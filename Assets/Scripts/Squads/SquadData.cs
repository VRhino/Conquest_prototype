using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Detailed configuration data for a squad type used by the game systems.
/// </summary>
[CreateAssetMenu(menuName = "Squads/Squad Data")]
public class SquadData : ScriptableObject
{
    // Identification
    /// <summary>Unique identifier for this squad type.</summary>
    public string id = string.Empty;
    /// <summary>Name of the squad shown in UI.</summary>
    public string squadName;
    /// <summary>Type of squad this data represents.</summary>
    public SquadType type;
    /// <summary>Unit type for this squad.</summary>
    public UnitType unitType;
    /// <summary>Icon used in menus.</summary>
    public Sprite icon;
    /// <summary>Background image for squad selection UI.</summary>
    public Sprite background;
    /// <summary>Image used in squad selection UI.</summary>
    public Sprite unitImage;
    /// <summary>Prefab spawned for this squad.</summary>
    public GameObject prefab;

    // Formations and leadership
    /// <summary>Grid-based formations the squad can adopt.</summary>
    public GridFormationScriptableObject[] gridFormations;
    /// <summary>Leadership cost to deploy the squad.</summary>
    public int leadershipCost;
    /// <summary>Default tactical behavior profile.</summary>
    public BehaviorProfile behaviorProfile;

    // Abilities
    /// <summary>Abilities unlocked as the squad levels up.</summary>
    public List<AbilityData> abilitiesByLevel;

    // Base unit attributes
    /// <summary>Base health for each unit.</summary>
    public int baseHealth;
    /// <summary>Base movement speed.</summary>
    public float baseSpeed;
    /// <summary>Mass used for physics and pushback.</summary>
    public float massValue;
    /// <summary>General weight category value.</summary>
    public float totalWeight;
    /// <summary>Block value if the unit carries a shield.</summary>
    public int block;
    /// <summary>Block regeneration rate.</summary>
    public int blockRegenRate;

    // Defenses
    /// <summary>Defense against slashing damage.</summary>
    public float slashingDefense;
    /// <summary>Defense against piercing damage.</summary>
    public float piercingDefense;
    /// <summary>Defense against blunt damage.</summary>
    public float bluntDefense;

    // Damage and penetration
    /// <summary>Slashing damage dealt.</summary>
    public float slashingDamage;
    /// <summary>Piercing damage dealt.</summary>
    public float piercingDamage;
    /// <summary>Blunt damage dealt.</summary>
    public float bluntDamage;
    /// <summary>Slashing penetration value.</summary>
    public float slashingPenetration;
    /// <summary>Piercing penetration value.</summary>
    public float piercingPenetration;
    /// <summary>Blunt penetration value.</summary>
    public float bluntPenetration;

    // Ranged-only attributes
    /// <summary>True if the squad attacks from range.</summary>
    public bool isDistanceUnit;
    /// <summary>Maximum effective range.</summary>
    public float range;
    /// <summary>Base accuracy percentage.</summary>
    public float accuracy;
    /// <summary>Time between shots.</summary>
    public float fireRate;
    /// <summary>Time required to reload.</summary>
    public float reloadSpeed;
    /// <summary>Total ammunition carried.</summary>
    public int ammo;

    // Progression curves
    /// <summary>Health scaling per level.</summary>
    public AnimationCurve healthCurve;
    /// <summary>Damage scaling per level.</summary>
    public AnimationCurve damageCurve;
    /// <summary>Defense scaling per level.</summary>
    public AnimationCurve defenseCurve;
    /// <summary>Speed scaling per level.</summary>
    public AnimationCurve speedCurve;

    // Squad size
    /// <summary>Total number of units in this squad.</summary>
    public int unitCount;
    
    /// <summary> Prefab name for the visual representation of this unit type.</summary>
    public string visualPrefabName;
}
