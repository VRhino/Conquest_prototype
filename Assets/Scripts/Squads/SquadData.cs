using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Detailed configuration data for a squad type used by the game systems.
/// </summary>
[CreateAssetMenu(menuName = "Squads/Squad Data")]
public class SquadData : ScriptableObject
{
    // Identification
    /// <summary>Name of the squad shown in UI.</summary>
    public string squadName;
    /// <summary>Type of squad this data represents.</summary>
    public SquadType tipo;
    /// <summary>Icon used in menus.</summary>
    public Sprite icon;
    /// <summary>Prefab spawned for this squad.</summary>
    public GameObject prefab;

    // Formations and leadership
    /// <summary>Formations the squad can adopt.</summary>
    public List<FormationType> availableFormations;
    /// <summary>Leadership cost to deploy the squad.</summary>
    public int liderazgoCost;
    /// <summary>Default tactical behavior profile.</summary>
    public BehaviorProfile behaviorProfile;

    // Abilities
    /// <summary>Abilities unlocked as the squad levels up.</summary>
    public List<AbilityData> abilitiesByLevel;

    // Base unit attributes
    /// <summary>Base health for each unit.</summary>
    public float vidaBase;
    /// <summary>Base movement speed.</summary>
    public float velocidadBase;
    /// <summary>Mass used for physics and pushback.</summary>
    public float masa;
    /// <summary>General weight category value.</summary>
    public float peso;
    /// <summary>Block value if the unit carries a shield.</summary>
    public float bloqueo;

    // Defenses
    /// <summary>Defense against slashing damage.</summary>
    public float defensaCortante;
    /// <summary>Defense against piercing damage.</summary>
    public float defensaPerforante;
    /// <summary>Defense against blunt damage.</summary>
    public float defensaContundente;

    // Damage and penetration
    /// <summary>Slashing damage dealt.</summary>
    public float danoCortante;
    /// <summary>Piercing damage dealt.</summary>
    public float danoPerforante;
    /// <summary>Blunt damage dealt.</summary>
    public float danoContundente;
    /// <summary>Slashing penetration value.</summary>
    public float penetracionCortante;
    /// <summary>Piercing penetration value.</summary>
    public float penetracionPerforante;
    /// <summary>Blunt penetration value.</summary>
    public float penetracionContundente;

    // Ranged-only attributes
    /// <summary>True if the squad attacks from range.</summary>
    public bool esUnidadADistancia;
    /// <summary>Maximum effective range.</summary>
    public float alcance;
    /// <summary>Base accuracy percentage.</summary>
    public float precision;
    /// <summary>Time between shots.</summary>
    public float cadenciaFuego;
    /// <summary>Time required to reload.</summary>
    public float velocidadRecarga;
    /// <summary>Total ammunition carried.</summary>
    public int municionTotal;

    // Progression curves
    /// <summary>Health scaling per level.</summary>
    public AnimationCurve vidaCurve;
    /// <summary>Damage scaling per level.</summary>
    public AnimationCurve danoCurve;
    /// <summary>Defense scaling per level.</summary>
    public AnimationCurve defensaCurve;
    /// <summary>Speed scaling per level.</summary>
    public AnimationCurve velocidadCurve;
}
