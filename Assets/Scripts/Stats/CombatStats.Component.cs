using Unity.Entities;

/// <summary>
/// Combat stats resolved from the modifier stack that the damage system reads on the attacker (Aprendizajes_Referencia.md
/// L1-13). Written only by <see cref="StatResolveSystem"/>.
/// </summary>
public struct CombatStatsComponent : IComponentData
{
    /// <summary>Fraction of the health a hit really takes that heals the attacker (<see cref="StatId.LifeSteal"/>).</summary>
    public float lifeSteal;
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

    public static SquadAIStatsComponent Neutral => new SquadAIStatsComponent { detectionRangeScale = 1f, combatRadiusScale = 1f };

    /// <summary>Stats that belong to the squad, not to its soldiers: a skill aimed at a soldier sends them to its squad.</summary>
    public static bool IsSquadStat(StatId stat)
        => stat == StatId.AIDetectionRange || stat == StatId.AICombatRadius || stat == StatId.AIHeroBias;
}
