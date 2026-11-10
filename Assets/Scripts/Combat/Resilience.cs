using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Resilience of a soldier or hero (Aprendizajes_Referencia.md L1-16; ref.: a max and a current value per actor that
/// each hit reaching health wears down). <see cref="max"/> and <see cref="controlResistance"/> are written only by
/// <see cref="StatResolveSystem"/> from <see cref="StatId.MaxResilience"/> and <see cref="StatId.ControlResistance"/>; the
/// rest only by <see cref="DamageCalculationSystem"/>. Max 0 = no resilience.
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
    /// <summary>Whole levels the target's reactions drop (L1-09, <see cref="StatId.ControlResistance"/>; written by StatResolveSystem).</summary>
    public float controlResistance;
    /// <summary>Last reaction (L1-09): a resilience break or a broken shield. The visuals play it when the revision changes.</summary>
    public HitReaction reaction;
    public uint reactionRevision;
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

    [Header("Reacciones (L1-09)")]
    [Tooltip("Aturdimiento de un derribo, s: lo que tarda en caer y levantarse (clip Hit_Knockdown). El retroceso usa stunDuration.")]
    [Min(0f)] public float knockdownDuration;
    [Tooltip("Aturdimiento de un despedido, s (clip Hit_Launch).")]
    [Min(0f)] public float launchDuration;
    [Tooltip("Empujón de un soldado derribado, m. El clip va en el sitio: esto es lo que lo mueve.")]
    [Min(0f)] public float knockdownPushDistance;
    [Tooltip("Duración de ese empujón, s.")]
    [Min(0f)] public float knockdownPushDuration;
    [Tooltip("Empujón de un soldado despedido, m.")]
    [Min(0f)] public float launchPushDistance;
    [Tooltip("Duración de ese empujón, s.")]
    [Min(0f)] public float launchPushDuration;

    public static ResilienceTuning Default => new()
    {
        stunDuration = 0.8f,
        recoveryDelay = 3f,
        recoveryPerSecond = 0.25f,
        criticalRatio = 0.5f,
        flankRatio = 0.25f,
        backRatio = 0.5f,
        ambushRatio = 0.5f,
        knockdownDuration = 2.2f,
        launchDuration = 2.6f,
        knockdownPushDistance = 0.8f,
        knockdownPushDuration = 0.45f,
        launchPushDistance = 2.5f,
        launchPushDuration = 0.6f,
    };

    /// <summary>Default values when the baked config predates these fields (all zero), field group by field group.</summary>
    public ResilienceTuning OrDefault()
    {
        var t = stunDuration > 0f ? this : Default;
        var d = Default;
        if (t.knockdownDuration <= 0f) t.knockdownDuration = d.knockdownDuration;
        if (t.launchDuration <= 0f) t.launchDuration = d.launchDuration;
        if (t.knockdownPushDuration <= 0f) { t.knockdownPushDistance = d.knockdownPushDistance; t.knockdownPushDuration = d.knockdownPushDuration; }
        if (t.launchPushDuration <= 0f) { t.launchPushDistance = d.launchPushDistance; t.launchPushDuration = d.launchPushDuration; }
        return t;
    }
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

    /// <summary>
    /// After a break (<see cref="Hit"/> returned true): the stun lasts what <paramref name="reaction"/> lasts and the
    /// reaction is published for the visuals. None (resisted): no stun, nothing to play; the bar is refilled anyway.
    /// </summary>
    public static void Break(ref ResilienceComponent r, HitReaction reaction, double now, in ResilienceTuning tuning)
    {
        r.stunnedUntil = now + HitReactions.StunDuration(reaction, tuning);
        if (reaction != HitReaction.None) Publish(ref r, reaction);
    }

    /// <summary>Publishes a reaction for the visuals (a broken shield publishes <see cref="HitReaction.GuardBreak"/>).</summary>
    public static void Publish(ref ResilienceComponent r, HitReaction reaction)
    {
        r.reaction = reaction;
        r.reactionRevision++;
    }
}
