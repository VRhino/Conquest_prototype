using UnityEngine;

/// <summary>
/// Curvas de progresión por nivel para un squad.
/// Requerido en <see cref="SquadData.progressionData"/>.
/// </summary>
[CreateAssetMenu(menuName = "Squads/Progression Data")]
public class SquadProgressionData : ScriptableObject
{
    [Header("Progression Curves (X = level, Y = multiplier)")]
    public AnimationCurve healthCurve   = AnimationCurve.Linear(1, 1, 30, 2);
    public AnimationCurve damageCurve   = AnimationCurve.Linear(1, 1, 30, 2);
    public AnimationCurve defenseCurve  = AnimationCurve.Linear(1, 1, 30, 2);
    public AnimationCurve speedCurve    = AnimationCurve.Linear(1, 1, 30, 1.5f);
}
