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
    /// <summary>Rarity level of this squad.</summary>
    public SquadRarity rarity = SquadRarity.levy_tier;

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

    // Combat parameters
    /// <summary>Melee attack range in world units.</summary>
    public float attackRange = 2f;
    /// <summary>Time in seconds between attacks.</summary>
    public float attackInterval = 1.5f;
    /// <summary>Probability of a critical hit (0–1).</summary>
    public float criticalChance = 0.05f;
    /// <summary>Damage multiplier applied on a critical hit.</summary>
    public float criticalMultiplier = 1.5f;

    /// <summary>Radius in world units within which enemies are detected.</summary>
    public float detectionRange = 8f;

    // Strike window timing
    /// <summary>Seconds from animation start when hitbox activates.</summary>
    public float strikeWindowStart = 0.35f;
    /// <summary>Duration the hitbox stays active.</summary>
    public float strikeWindowDuration = 0.15f;
    /// <summary>Total duration of the attack animation.</summary>
    public float attackAnimationDuration = 1.0f;

    // Kinetic
    /// <summary>Scales penetration bonus from attacker speed.</summary>
    public float kineticMultiplier = 0.3f;

    // Squad size
    /// <summary>Total number of units in this squad.</summary>
    public int unitCount;
    
    /// <summary> Prefab name for the visual representation of this unit type.</summary>
    public string visualPrefabName;
}
