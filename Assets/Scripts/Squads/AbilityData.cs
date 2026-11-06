using UnityEngine;

/// <summary>
/// A squad ability unlocked through progression (GDD 4.6), launched by the squad through the same gate, phases and
/// effects as hero skills (Aprendizajes_Referencia.md L2-9). Turned into a <see cref="SkillBlob"/> when the squad is
/// baked; its slot is its place in <see cref="SquadData.abilitiesByLevel"/>.
/// </summary>
[CreateAssetMenu(menuName = "Squads/Ability Data")]
public class AbilityData : ScriptableObject
{
    /// <summary>Name displayed for the ability.</summary>
    public string abilityName;
    /// <summary>Icon used in UI.</summary>
    public Sprite icon;
    /// <summary>Text description of the effect.</summary>
    public string description;
    /// <summary>Cooldown in seconds before reuse.</summary>
    public float cooldown;

    [Header("Lifecycle (empty = one short cast phase)")]
    [Tooltip("Fases en orden. Duración negativa = sin fin (espera de carga); la final es la última.")]
    public SkillPhase[] phases = new SkillPhase[0];

    [Tooltip("Eventos por fase e instante. Para una escuadra, Self y OwnSquad son sus soldados; el área sale del centro de la escuadra hacia donde mira.")]
    public SkillEvent[] events = new SkillEvent[0];

    [Tooltip("Grupo de enfriamiento de la escuadra; 0 = el de su hueco.")]
    public int cooldownGroup;

    [Tooltip("Cada soldado la ejecuta por su cuenta (L2-17): la orden se escalona y cada uno lanza sus eventos desde su " +
             "posición y hacia donde mira. Para acciones (golpes); las mejoras de toda la escuadra van sin marcar.")]
    public bool perSoldier;
}
