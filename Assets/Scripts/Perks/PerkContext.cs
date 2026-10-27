using Unity.Entities;

/// <summary>
/// Gathers what perk conditions look at (L2-15) for <see cref="PerkAuraSystem"/> and <see cref="PerkOnHitSystem"/>, so
/// <see cref="PerkConditions"/> stays pure.
/// </summary>
public static class PerkContext
{
    public static PerkSourceContext Source(EntityManager em, Entity hero, in PerkEffect effect, double now, float roll)
    {
        var context = new PerkSourceContext
        {
            motion = em.HasComponent<HeroMotionComponent>(hero) ? em.GetComponentData<HeroMotionComponent>(hero) : default,
            roll = roll,
            selfHasEffect = em.HasBuffer<StatModifier>(hero)
                && PerkConditions.HasActiveEffect(em.GetBuffer<StatModifier>(hero, true), effect.effectStat, now),
        };
        if (!em.HasComponent<HeroSquadReference>(hero)) return context;
        var squad = em.GetComponentData<HeroSquadReference>(hero).squad;
        if (!em.Exists(squad) || !em.HasBuffer<SquadUnitElement>(squad) || !em.HasComponent<SquadInstanceComponent>(squad)) return context;
        int initial = em.GetComponentData<SquadInstanceComponent>(squad).initialUnitCount;
        if (initial <= 0) return context;
        int alive = 0;
        foreach (var unit in em.GetBuffer<SquadUnitElement>(squad, true))
            if (em.Exists(unit.Value) && !em.HasComponent<IsDeadComponent>(unit.Value)) alive++;
        context.hasSquad = true;
        context.squadAliveFraction = (float)alive / initial;
        return context;
    }

    public static PerkTargetContext Target(EntityManager em, Entity target, StatId effectStat, double now)
    {
        var context = new PerkTargetContext
        {
            isHero = em.HasComponent<HeroLifeComponent>(target),
            hasEffect = em.HasBuffer<StatModifier>(target)
                && PerkConditions.HasActiveEffect(em.GetBuffer<StatModifier>(target, true), effectStat, now),
        };
        if (em.HasComponent<UnitOwnerComponent>(target))
        {
            context.isSoldier = true;
            var squad = em.GetComponentData<UnitOwnerComponent>(target).squad;
            if (em.Exists(squad) && em.HasComponent<SquadDefinitionComponent>(squad))
                context.squadType = em.GetComponentData<SquadDefinitionComponent>(squad).squadType;
        }
        return context;
    }
}
