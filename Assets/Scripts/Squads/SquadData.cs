using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Configuración base de un squad. Los módulos opcionales (<see cref="meleeData"/>,
/// <see cref="rangedData"/>) habilitan los comportamientos correspondientes cuando están asignados.
/// </summary>
[CreateAssetMenu(menuName = "Squads/Squad Data")]
public class SquadData : ScriptableObject
{
    // ── Combat Modules ────────────────────────────────────────────────────────
    [Header("Combat Modules")]
    /// <summary>Datos de combate melee. Si es null el squad no puede atacar cuerpo a cuerpo.</summary>
    public SquadMeleeData meleeData;
    /// <summary>Datos de combate ranged. Si es null el squad no puede atacar a distancia.</summary>
    public SquadRangedData rangedData;

    [Header("Progression")]
    /// <summary>Curvas de progresión por nivel para este squad.</summary>
    public SquadProgressionData progressionData;

    /// <summary>True si el squad puede atacar a distancia (rangedData asignado).</summary>
    public bool IsRanged => rangedData != null;
    /// <summary>True si el squad puede atacar en melee (meleeData asignado).</summary>
    public bool IsMelee  => meleeData  != null;

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
    /// <summary>Stun duration in seconds when the shield is broken.</summary>
    public float shieldBreakStunDuration;

    // Defenses
    /// <summary>Defense against slashing damage.</summary>
    public float slashingDefense;
    /// <summary>Defense against piercing damage.</summary>
    public float piercingDefense;
    /// <summary>Defense against blunt damage.</summary>
    public float bluntDefense;

    // ── Detection (base — compartido por melee y ranged) ─────────────────────
    /// <summary>Radius in world units within which enemies are detected.</summary>
    public float detectionRange = 8f;

    // Squad size
    /// <summary>Total number of units in this squad.</summary>
    public int unitCount;
    
    /// <summary> Prefab name for the visual representation of this unit type.</summary>
    public string visualPrefabName;
}
