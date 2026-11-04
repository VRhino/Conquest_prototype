using System;
using UnityEngine;

/// <summary>
/// When a squad's formation counts as ready (F12) and how long an AI hero waits for it before charging.
/// Baked in <see cref="SquadSpawnConfigComponent"/>; read by <see cref="SquadAISystem"/> and the hero AI behaviors.
/// </summary>
[Serializable]
public struct FormationReadinessTuning
{
    [Tooltip("Fracción de soldados que tiene que estar en su ranura para que la formación esté lista (F12).")]
    [Range(0f, 1f)] public float quorum;
    [Tooltip("Un soldado a más de esta distancia de su ranura resuelta no ha llegado, m.")]
    [Min(0f)] public float stragglerDistance;
    [Tooltip("Lo más que espera un héroe de IA a su formación antes de mandar cargar, s.")]
    [Min(0f)] public float heroMaxWait;
    [Tooltip("Con un héroe enemigo más cerca que esto, el héroe de IA carga sin esperar, m.")]
    [Min(0f)] public float heroWaitEnemyDistance;

    public static FormationReadinessTuning Default => new()
    {
        quorum = 0.8f,
        stragglerDistance = 1.5f,
        heroMaxWait = 3f,
        heroWaitEnemyDistance = 8f,
    };

    /// <summary>Default values when the baked config predates these fields (all zero).</summary>
    public FormationReadinessTuning OrDefault() => stragglerDistance > 0f ? this : Default;
}
