using UnityEngine;

/// <summary>
/// Datos de combate cuerpo a cuerpo para un squad.
/// Asignar este asset a <see cref="SquadData.meleeData"/> habilita el comportamiento melee.
/// </summary>
[CreateAssetMenu(menuName = "Squads/Melee Data")]
public class SquadMeleeData : ScriptableObject
{
    [Header("Damage")]
    public float slashingDamage;
    public float piercingDamage;
    public float bluntDamage;

    [Header("Penetration")]
    public float slashingPenetration;
    public float piercingPenetration;
    public float bluntPenetration;

    [Header("Combat Timing")]
    public float attackRange = 2f;
    public float attackInterval = 1.5f;
    public float strikeWindowStart = 0.35f;
    public float strikeWindowDuration = 0.15f;
    public float attackAnimationDuration = 1.0f;

    [Header("Modifiers")]
    public float criticalChance = 0.05f;
    public float criticalMultiplier = 1.5f;
    public float kineticMultiplier = 0.3f;
}
