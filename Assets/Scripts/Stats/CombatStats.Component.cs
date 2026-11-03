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
