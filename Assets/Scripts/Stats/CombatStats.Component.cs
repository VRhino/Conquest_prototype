using Unity.Entities;

/// <summary>
/// Combat stats resolved from the modifier stack that the damage system reads on the attacker (Aprendizajes_Referencia.md
/// L1-13). Written only by <see cref="StatResolveSystem"/>.
/// </summary>
public struct CombatStatsComponent : IComponentData
{
    /// <summary>Fraction of the health a hit really takes that heals the attacker (<see cref="StatId.LifeSteal"/>).</summary>
    public float lifeSteal;
    /// <summary>Multipliers of the duration of the effects it applies and receives (L2-10).</summary>
    public float effectDurationOut;
    public float effectDurationIn;
    /// <summary>Attack speed (L2-12); 0 = not yet resolved, read as 1 (<see cref="AttackSpeedOf"/>).</summary>
    public float attackSpeed;

    public static float AttackSpeedOf(bool has, in CombatStatsComponent stats) => has && stats.attackSpeed > 0f ? stats.attackSpeed : 1f;

    /// <summary>Duration of an effect from <paramref name="caster"/> on <paramref name="target"/>: both sides scale it (L2-10).</summary>
    public static float EffectDuration(float duration, bool casterHas, in CombatStatsComponent caster, bool targetHas,
        in CombatStatsComponent target)
        => duration * (casterHas ? Scale(caster.effectDurationOut) : 1f) * (targetHas ? Scale(target.effectDurationIn) : 1f);

    /// <summary>A multiplier not yet resolved (0) counts as 1.</summary>
    static float Scale(float multiplier) => multiplier > 0f ? multiplier : 1f;
}

/// <summary>
/// Squad AI parameters resolved from the squad entity's modifier stack (Aprendizajes_Referencia.md L4-10; ref.: effects
/// can change the parameters of the AI strategy they target). Written only by <see cref="StatResolveSystem"/>; read by
/// <see cref="EnemyDetectionSystem"/> and <see cref="UnitTargetingSystem"/>. A squad without it uses <see cref="Neutral"/>.
/// </summary>
public struct SquadAIStatsComponent : IComponentData
{
    public float detectionRangeScale;
    public float combatRadiusScale;
    public float heroBias;
    /// <summary>Multiplier of the stamina each running soldier spends (M18); read by <see cref="SquadStaminaSystem"/>.</summary>
    public float staminaCostScale;

    public static SquadAIStatsComponent Neutral => new SquadAIStatsComponent
    {
        detectionRangeScale = 1f, combatRadiusScale = 1f, staminaCostScale = 1f,
    };

    /// <summary>Stats that belong to the squad, not to its soldiers: a skill aimed at a soldier sends them to its squad.</summary>
    public static bool IsSquadStat(StatId stat)
        => stat == StatId.AIDetectionRange || stat == StatId.AICombatRadius || stat == StatId.AIHeroBias
            || stat == StatId.StaminaCost;
}
