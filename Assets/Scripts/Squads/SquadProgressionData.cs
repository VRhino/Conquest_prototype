using UnityEngine;

/// <summary>
/// Curvas de progresión por nivel para un squad, y su tabla de experiencia y desbloqueos (Aprendizajes_Referencia.md
/// L7-5): el balance se cambia aquí, sin recompilar.
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

    [Header("Experience and unlocks")]
    [Tooltip("XP para pasar del nivel n al n+1 (elemento 0 = nivel 1). Un hueco o un 0 usa 100 × 1,1^(n−1).")]
    public float[] xpToNextLevel = DefaultXPTable();

    [Tooltip("Nivel en que se desbloquea cada habilidad de la escuadra, en orden. Sin entrada: cada 10 niveles.")]
    public int[] abilityUnlockLevels = { 10, 20, 30 };

    static float[] DefaultXPTable()
    {
        var table = new float[SquadProgressionCurves.MaxLevel - 1];
        for (int i = 0; i < table.Length; i++) table[i] = SquadProgressionCurves.DefaultXPToNext(i + 1);
        return table;
    }
}
