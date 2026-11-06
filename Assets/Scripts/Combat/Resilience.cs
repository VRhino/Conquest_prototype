using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Resilience of a soldier or hero (Aprendizajes_Referencia.md L1-16; ref.: a max and a current value per actor that
/// each hit reaching health wears down). <see cref="max"/> is written only by <see cref="StatResolveSystem"/> from
/// <see cref="StatId.MaxResilience"/>; the rest only by <see cref="DamageCalculationSystem"/>. Max 0 = no resilience.
/// </summary>
public struct ResilienceComponent : IComponentData
{
    public float max;
    /// <summary>Value right after the last hit; the recovery since then is computed when read (<see cref="Resilience.Current"/>).</summary>
    public float current;
    /// <summary>Simulation time of the last hit that wore it down.</summary>
    public double lastHitTime;
    /// <summary>Simulation time the stun of a break ends; until then hits do not wear it down.</summary>
    public double stunnedUntil;
}

/// <summary>How resilience wears down, recovers and breaks (L1-16), in <see cref="SquadSpawnConfigComponent"/>.</summary>
[Serializable]
public struct ResilienceTuning
{
    [Tooltip("Aturdimiento al vaciarse la resiliencia: sin moverse, sin atacar y sin habilidades, s.")]
    [Min(0f)] public float stunDuration;
    [Tooltip("Segundos sin recibir golpes antes de que la resiliencia empiece a recuperarse.")]
    [Min(0f)] public float recoveryDelay;
    [Tooltip("Fracción de la resiliencia máxima que se recupera por segundo.")]
    [Min(0f)] public float recoveryPerSecond;
    [Tooltip("Resiliencia extra que quita un crítico, como ratio: 0,5 = +50 %.")]
    public float criticalRatio;
    [Tooltip("Resiliencia extra que quita un golpe de flanco (L1-15), como ratio.")]
    public float flankRatio;
    [Tooltip("Resiliencia extra que quita un golpe por la espalda (L1-15), como ratio.")]
    public float backRatio;
    [Tooltip("Resiliencia extra que quita una emboscada (L1-15), como ratio; se suma a la de la dirección.")]
    public float ambushRatio;

    public static ResilienceTuning Default => new()
    {
        stunDuration = 0.8f,
        recoveryDelay = 3f,
        recoveryPerSecond = 0.25f,
        criticalRatio = 0.5f,
        flankRatio = 0.25f,
        backRatio = 0.5f,
        ambushRatio = 0.5f,
    };

    /// <summary>Default values when the baked config predates these fields (all zero).</summary>
    public ResilienceTuning OrDefault() => stunDuration > 0f ? this : Default;
}

/// <summary>
/// Pure rules of resilience (L1-16; ref.: the hit's resilience damage comes from the weapon and grows with critical and
/// ambush hits). The recovery is lazy: nothing runs per frame, the value is brought up to date when a hit reads it.
/// </summary>
public static class Resilience
{
    /// <summary>The value at <paramref name="now"/>: the stored one plus what it recovered since the delay after the last hit.</summary>
    public static float Current(in ResilienceComponent r, double now, in ResilienceTuning tuning)
    {
        double recovering = now - r.lastHitTime - tuning.recoveryDelay;
        if (recovering <= 0) return math.min(r.current, r.max);
        return math.min(r.max, r.current + r.max * tuning.recoveryPerSecond * (float)recovering);
    }

    /// <summary>Resilience a hit takes: the weapon's, times 1 + the critical, direction and ambush ratios added.</summary>
    public static float HitDamage(float weaponDamage, bool critical, HitDirection direction, bool ambush, in ResilienceTuning tuning)
    {
        if (weaponDamage <= 0f) return 0f;
        float ratio = (critical ? tuning.criticalRatio : 0f)
                    + direction switch
                    {
                        HitDirection.Flank => tuning.flankRatio,
                        HitDirection.Back => tuning.backRatio,
                        _ => 0f,
                    }
                    + (ambush ? tuning.ambushRatio : 0f);
        return weaponDamage * math.max(0f, 1f + ratio);
    }

    public static bool IsStunned(in ResilienceComponent r, double now) => now < r.stunnedUntil;

    /// <summary>
    /// Wears <paramref name="r"/> down by <paramref name="damage"/>. True when it breaks: it is then stunned until
    /// <see cref="ResilienceComponent.stunnedUntil"/> and refilled. No resilience, no damage or already stunned: nothing.
    /// </summary>
    public static bool Hit(ref ResilienceComponent r, float damage, double now, in ResilienceTuning tuning)
    {
        if (r.max <= 0f || damage <= 0f || IsStunned(r, now)) return false;
        r.current = Current(r, now, tuning) - damage;
        r.lastHitTime = now;
        if (r.current > 0f) return false;
        r.current = r.max;
        r.stunnedUntil = now + tuning.stunDuration;
        return true;
    }
}
