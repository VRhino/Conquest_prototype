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
    [Tooltip("Shown in barracks for every hero, including existing saves. Does not grant a recruited instance.")]
    public bool availableForRecruitmentByDefault;
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
    /// <summary>Formations the squad can adopt (F1, F2…).</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("gridFormations")]
    public FormationDefinition[] formations;
    /// <summary>
    /// «En reposo»: the formation the squad takes while it follows the hero (type <see cref="FormationType.Rest"/>).
    /// Always available; it is not one of the F1-F4 formations. Levies stand loose, drilled troops close together.
    /// </summary>
    [Tooltip("«En reposo»: formación al seguir al héroe. Siempre disponible; no entra en F1-F4 ni en el ciclo con X.")]
    public FormationDefinition restFormation;
    /// <summary>Leadership cost to deploy the squad.</summary>
    public int leadershipCost;
    /// <summary>Default tactical behavior profile.</summary>
    public BehaviorProfile behaviorProfile;

    // Abilities
    /// <summary>Abilities unlocked as the squad levels up.</summary>
    public List<AbilityData> abilitiesByLevel;

    [Header("Cuerpo (L5-5)")]
    [Tooltip("Radio del cuerpo de cada soldado, m. 0 = el de MovementTuningConfig (0,5 m). Las unidades grandes (caballería) llevan más.")]
    [Min(0f)] public float bodyRadius;

    [Header("Montura (L5-8; solo caballería)")]
    [Tooltip("Arco total, en grados, respecto al frente del caballo en el que el jinete puede golpear o disparar. 0 = sin límite.")]
    [Range(0f, 360f)] public float mountAttackArc;
    [Tooltip("Giro del caballo en combate, °/s. 0 = el de MovementTuningConfig (combatTurnRate), que es el de un soldado a pie.")]
    [Min(0f)] public float mountTurnRate;

    [Header("Aguante de escuadra (L5-7)")]
    [Tooltip("Aguante máximo de la escuadra. 0 = sin aguante: corre sin coste, como antes.")]
    [Min(0f)] public float staminaMax;
    [Tooltip("Aguante por segundo que gasta cada soldado que corre.")]
    [Min(0f)] public float staminaRunCostPerSoldier;
    [Tooltip("Aguante por segundo que recupera la escuadra mientras nadie corre.")]
    [Min(0f)] public float staminaRecoveryPerSecond;
    [Tooltip("Fracción del máximo que hay que recuperar, tras agotarse, para volver a correr.")]
    [Range(0f, 1f)] public float staminaRecoverFraction = 0.3f;

    [Header("Modificadores condicionados (L1-02)")]
    [Tooltip("Daño extra (o reducido) que el tipo de escuadra hace o recibe solo cuando se cumple la condición del golpe.")]
    public List<ConditionalDamage> conditionalModifiers = new List<ConditionalDamage>();

    [Header("Terreno (L1-14)")]
    [Tooltip("Modificadores de vida, daño, defensa o velocidad de sus soldados según el terreno del mapa.")]
    public List<TerrainModifier> terrainModifiers = new List<TerrainModifier>();

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
    /// <summary>Resilience of each soldier (L1-16): hits that reach health wear it down; empty = a short stun. 0 = none.</summary>
    [Tooltip("Resiliencia de cada soldado (L1-16): los golpes que llegan a la vida la gastan y al vaciarse queda aturdido un momento. 0 = sin resiliencia.")]
    [Min(0f)] public float baseResilience;

    // Defenses
    /// <summary>Defense against slashing damage.</summary>
    public float slashingDefense;
    /// <summary>Defense against piercing damage.</summary>
    public float piercingDefense;
    /// <summary>Defense against blunt damage.</summary>
    public float bluntDefense;
    /// <summary>Base share of each damage type taken off after the flat defense (L1-19), capped by CombatTuning.maxRelief.</summary>
    [Tooltip("Parte del daño contundente que se quita tras la defensa plana (0,2 = 20 %; tope 0,75). Base de la pila; perks y efectos suman.")]
    [Min(0f)] public float bluntRelief;
    [Tooltip("Parte del daño cortante que se quita tras la defensa plana (0,2 = 20 %; tope 0,75).")]
    [Min(0f)] public float slashRelief;
    [Tooltip("Parte del daño perforante que se quita tras la defensa plana (0,2 = 20 %; tope 0,75).")]
    [Min(0f)] public float pierceRelief;

    // ── Detection (base — compartido por melee y ranged) ─────────────────────
    /// <summary>Radius in world units within which enemies are detected.</summary>
    public float detectionRange = 8f;

    // Squad size
    /// <summary>Total number of units in this squad.</summary>
    public int unitCount;
    
    /// <summary> Prefab name for the visual representation of this unit type.</summary>
    public string visualPrefabName;
}
