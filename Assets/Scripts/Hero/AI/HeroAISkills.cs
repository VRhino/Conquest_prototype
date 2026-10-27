using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>Tuning of the AI's use of skills (H1) and of its choice of an ally to support (L4-08).</summary>
[Serializable]
public struct HeroAISkillTuning
{
    [Tooltip("Soldados enemigos que tiene que haber al alcance de una habilidad ofensiva para lanzarla (un héroe enemigo basta siempre).")]
    [Min(1)] public int minEnemyUnits;
    [Tooltip("Radio en el que la IA busca un aliado al que apoyar (L4-08).")]
    [Min(0f)] public float allySearchRadius;
    [Tooltip("Peso de la cercanía en la puntuación del aliado (1 pegado, 0 en el borde del radio).")]
    public float allyDistanceWeight;
    [Tooltip("Peso de la vida perdida (fracción 0-1) en la puntuación del aliado.")]
    public float allyHurtWeight;
    [Tooltip("Bonus a la puntuación de un aliado que es héroe.")]
    public float allyHeroWeight;
    [Tooltip("Bonus del aliado ya elegido: otro tiene que superarlo en esto para quitarle el puesto.")]
    public float allyHysteresis;
    [Tooltip("Vida perdida (fracción) desde la que el aliado elegido justifica una habilidad de apoyo en área.")]
    [Range(0f, 1f)] public float supportMinMissingHealth;

    public static HeroAISkillTuning Default => new()
    {
        minEnemyUnits = 3,
        allySearchRadius = 15f,
        allyDistanceWeight = 1f,
        allyHurtWeight = 2f,
        allyHeroWeight = 0.5f,
        allyHysteresis = 0.25f,
        supportMinMissingHealth = 0.25f,
    };

    /// <summary>Default values when the baked config predates these fields (all zero).</summary>
    public HeroAISkillTuning OrDefault() => allySearchRadius > 0f ? this : Default;
}

/// <summary>What a skill is for, from its effect events, so the AI knows when to use it.</summary>
public enum AISkillPurpose : byte
{
    None,
    /// <summary>Hurts or weakens enemies: cast with enemies in reach.</summary>
    Offensive,
    /// <summary>Helps the caster or its squad: cast while fighting.</summary>
    SupportSelf,
    /// <summary>Helps allies around the caster: cast when the chosen ally is in reach and hurt (L4-08).</summary>
    SupportAllies,
}

/// <summary>An ally the AI could support (L4-08).</summary>
public struct AllyCandidate
{
    public Entity entity;
    public float distance;
    /// <summary>Health lost, 0-1.</summary>
    public float missingHealth;
    public bool isHero;
}

/// <summary>
/// The AI's use of skills (H1) and its choice of an ally (L4-08; ref.: the support AI scores allies by closeness, health
/// lost and being a hero within a radius, and keeps the chosen one while it stays valid and none is clearly better).
/// Pure: the system gathers counts and candidates.
/// </summary>
public static class HeroAISkills
{
    /// <summary>
    /// What the skill does, and how far from the caster it reaches (an area's centre distance plus its radius). Any
    /// damage or effect on enemies makes it offensive; otherwise effects on allies in a radius make it a support of allies.
    /// </summary>
    public static AISkillPurpose PurposeOf(ref SkillBlob skill, out float reach)
    {
        reach = 0f;
        bool offensive = false, allies = false, self = false;
        for (int i = 0; i < skill.events.Length; i++)
        {
            ref var e = ref skill.events[i];
            bool effect = e.type == SkillEventType.ApplyModifier || e.type == SkillEventType.AbsorbShield;
            if (e.type == SkillEventType.AreaDamage || (effect && e.target == SkillTarget.EnemiesInRadius))
            {
                offensive = true;
                reach = math.max(reach, e.distance + e.radius);
            }
            else if (effect && e.target == SkillTarget.AlliesInRadius)
            {
                allies = true;
                reach = math.max(reach, e.radius);
            }
            else if (effect) self = true;
        }
        if (offensive) return AISkillPurpose.Offensive;
        if (allies) return AISkillPurpose.SupportAllies;
        return self ? AISkillPurpose.SupportSelf : AISkillPurpose.None;
    }

    /// <summary>Whether a ready skill of <paramref name="purpose"/> is worth casting now.</summary>
    public static bool ShouldCast(AISkillPurpose purpose, int enemyHeroesInReach, int enemyUnitsInReach, bool fighting,
        bool allyInReach, float allyMissingHealth, in HeroAISkillTuning tuning) => purpose switch
    {
        AISkillPurpose.Offensive => enemyHeroesInReach > 0 || enemyUnitsInReach >= tuning.minEnemyUnits,
        AISkillPurpose.SupportSelf => fighting,
        AISkillPurpose.SupportAllies => allyInReach && allyMissingHealth >= tuning.supportMinMissingHealth,
        _ => false,
    };

    /// <summary>Score of an ally inside the search radius: closeness, health lost and being a hero; -1 outside it.</summary>
    public static float AllyScore(in AllyCandidate ally, in HeroAISkillTuning tuning)
    {
        if (tuning.allySearchRadius <= 0f || ally.distance > tuning.allySearchRadius) return -1f;
        return tuning.allyDistanceWeight * (1f - ally.distance / tuning.allySearchRadius)
               + tuning.allyHurtWeight * math.saturate(ally.missingHealth)
               + (ally.isHero ? tuning.allyHeroWeight : 0f);
    }

    /// <summary>
    /// The ally to support: the best scored, with the current one ahead by <see cref="HeroAISkillTuning.allyHysteresis"/>
    /// while it is still a candidate. Entity.Null when none is in the radius.
    /// </summary>
    public static Entity PickAlly(ReadOnlySpan<AllyCandidate> candidates, Entity current, in HeroAISkillTuning tuning, out AllyCandidate picked)
    {
        picked = default;
        float best = -1f;
        foreach (var c in candidates)
        {
            float score = AllyScore(c, tuning);
            if (score < 0f) continue;
            if (c.entity == current) score += tuning.allyHysteresis;
            if (score > best)
            {
                best = score;
                picked = c;
            }
        }
        return best < 0f ? Entity.Null : picked.entity;
    }
}
