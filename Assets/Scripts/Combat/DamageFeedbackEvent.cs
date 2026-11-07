using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Presentation event of a resolved hit (L1-03): where to show which floating text. Created by
/// <see cref="DamageCalculationSystem"/>, which no longer calls UI; consumed by
/// <see cref="DamageFeedbackPresentationSystem"/>.
/// </summary>
public struct DamageFeedbackEvent : IComponentData
{
    /// <summary>World position of the target when it was hit.</summary>
    public float3 position;
    public DamageCategory category;
    public float amount;
    /// <summary>Who dealt the hit (hero, soldier, squad or caster) and who took it: whether it is the local player's.</summary>
    public Entity source;
    public Entity target;
}

/// <summary>
/// Which floating texts a player sees in a battle of many squads (C12): the hits its hero or its soldiers deal or take,
/// and of the rest only the categories marked as important (<see cref="FCTCategoryEntry.showForOthers"/>), never more than
/// a cap at once.
/// </summary>
public static class FloatingTextRelevance
{
    /// <summary>The hero an entity fights for: itself, the hero of its squad, or of the squad it belongs to.</summary>
    public static Entity OwnerHero(EntityManager em, Entity e)
    {
        if (e == Entity.Null || !em.Exists(e)) return Entity.Null;
        if (em.HasComponent<UnitOwnerComponent>(e)) return em.GetComponentData<UnitOwnerComponent>(e).hero;
        if (em.HasComponent<SquadOwnerComponent>(e)) return em.GetComponentData<SquadOwnerComponent>(e).hero;
        return e;
    }

    public static bool IsLocal(EntityManager em, Entity e)
    {
        var hero = OwnerHero(em, e);
        return hero != Entity.Null && em.Exists(hero) && em.HasComponent<IsLocalPlayer>(hero);
    }

    /// <summary>The local player's hero or soldiers dealt or took the hit.</summary>
    public static bool InvolvesLocal(EntityManager em, in DamageFeedbackEvent e) => IsLocal(em, e.source) || IsLocal(em, e.target);

    /// <summary>Whether a text is shown: the player's, or important, and only while under the cap of texts at once.</summary>
    public static bool Shows(bool involvesLocal, bool showForOthers, int active, int maxActive)
        => (involvesLocal || showForOthers) && active < maxActive;
}
