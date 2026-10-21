using UnityEngine;

/// <summary>
/// Data defining an active hero ability used during combat.
/// Loaded by <see cref="HeroClassDefinition"/> and referenced by the HUD. Turned into a <see cref="SkillBlob"/> by
/// <see cref="SkillBlobBuilder"/> when the hero is initialised (Aprendizajes_Referencia.md L2-2).
/// </summary>
[CreateAssetMenu(menuName = "Hero/Ability Data")]
public class HeroAbility : ScriptableObject
{
    /// <summary>Name shown to players.</summary>
    public string abilityName;

    /// <summary>Icon representing the ability in UI.</summary>
    public Sprite icon;

    /// <summary>Tooltip or descriptive text.</summary>
    public string description;

    /// <summary>Cooldown time in seconds before the ability can be reused.</summary>
    public float cooldown;

    /// <summary>Stamina required to activate the ability.</summary>
    public float staminaCost;

    /// <summary>Button slot this ability belongs to (Q, E, R or Ultimate).</summary>
    public HeroAbilityCategory category;

    /// <summary>Optional animation tag triggered when used.</summary>
    public string animationTag;

    /// <summary>Optional radius for area based effects.</summary>
    public float areaRadius;

    /// <summary>Optional multiplier applied to base damage.</summary>
    public float damageMultiplier;

    [Header("Lifecycle (empty = windup, active and recovery by default)")]
    [Tooltip("Fases en orden. Duración negativa = sin fin (espera de carga); la final es la última.")]
    public SkillPhase[] phases = new SkillPhase[0];

    [Tooltip("Eventos por fase e instante: animación, golpe, daño en área, modificador, embestida, escudo, proyectil.")]
    public SkillEvent[] events = new SkillEvent[0];

    [Tooltip("Interrupt sustituye a la habilidad en curso; Yield la aparca y la restaura al terminar.")]
    public SkillRefresh refresh = SkillRefresh.Interrupt;

    [Tooltip("Grupo de enfriamiento compartido; 0 = el de su tecla.")]
    public int cooldownGroup;
}
