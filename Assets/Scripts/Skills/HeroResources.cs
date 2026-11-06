using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// The hero's fighting resources (Aprendizajes_Referencia.md L2-16; ref.: a combo-point actor property and a soldier
/// kill count that the launch gate checks with reasons of their own). Gained by <see cref="DamageCalculationSystem"/> when
/// the hero's hits land and kill; spent by <see cref="SkillExecutionSystem"/> when a skill that costs them starts.
/// </summary>
public struct HeroResourcesComponent : IComponentData
{
    /// <summary>Combo right after the last hit that landed; the decay since then is computed when read (<see cref="HeroResources.Combo"/>).</summary>
    public float combo;
    /// <summary>Simulation time of the last hit that landed.</summary>
    public double lastHitTime;
    /// <summary>Soldiers killed and not yet spent.</summary>
    public int kills;
}

/// <summary>How the combo is gained and lost (L2-16), in <see cref="HeroGameplayConfigComponent"/>.</summary>
[Serializable]
public struct ComboTuning
{
    [Tooltip("Combo que gana el héroe con cada golpe suyo que llega a la vida (melé, flecha o habilidad).")]
    [Min(0f)] public float gainPerHit;
    [Tooltip("Combo máximo.")]
    [Min(0f)] public float max;
    [Tooltip("Segundos sin acertar antes de que el combo empiece a perderse.")]
    [Min(0f)] public float decayDelay;
    [Tooltip("Combo que se pierde por segundo pasado ese plazo.")]
    [Min(0f)] public float decayPerSecond;

    public static ComboTuning Default => new() { gainPerHit = 1f, max = 30f, decayDelay = 6f, decayPerSecond = 5f };

    /// <summary>Default values when the baked config predates these fields (all zero).</summary>
    public ComboTuning OrDefault() => max > 0f ? this : Default;
}

/// <summary>Pure rules of the hero's combo and kills (L2-16). The decay is lazy: nothing runs per frame.</summary>
public static class HeroResources
{
    /// <summary>The combo at <paramref name="now"/>: the stored one minus what it lost since the delay after the last hit.</summary>
    public static float Combo(in HeroResourcesComponent r, double now, in ComboTuning tuning)
    {
        double decaying = now - r.lastHitTime - tuning.decayDelay;
        float lost = decaying > 0 ? tuning.decayPerSecond * (float)decaying : 0f;
        return math.clamp(r.combo - lost, 0f, tuning.max);
    }

    /// <summary>A hit of the hero landed: the combo brought up to date, plus the gain, capped; the clock restarts.</summary>
    public static void Landed(ref HeroResourcesComponent r, double now, in ComboTuning tuning)
    {
        r.combo = math.min(tuning.max, Combo(r, now, tuning) + tuning.gainPerHit);
        r.lastHitTime = now;
    }

    public static bool CanPay(in HeroResourcesComponent r, double now, in ComboTuning tuning, float comboCost, int killCost)
        => Combo(r, now, tuning) >= comboCost && r.kills >= killCost;

    /// <summary>
    /// Spends the costs of a skill that starts. The combo left keeps decaying from the last hit, so it is stored as if
    /// that hit had left it there.
    /// </summary>
    public static void Pay(ref HeroResourcesComponent r, double now, in ComboTuning tuning, float comboCost, int killCost)
    {
        if (comboCost > 0f)
        {
            float left = math.max(0f, Combo(r, now, tuning) - comboCost);
            double decaying = now - r.lastHitTime - tuning.decayDelay;
            r.combo = left + (decaying > 0 ? tuning.decayPerSecond * (float)decaying : 0f);
        }
        r.kills = math.max(0, r.kills - killCost);
    }
}
