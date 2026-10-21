using Unity.Entities;

/// <summary>
/// Helpers for the code that owns a stat source (spawn, level, equipment) to seed bases and replace its own modifiers.
/// They never write the derived components: <see cref="StatResolveSystem"/> does, when <see cref="ModifiersDirty"/> is on.
/// Main-thread EntityManager use only (structural changes add the buffers the first time).
/// </summary>
public static class StatStack
{
    /// <summary>Adds the base and modifier buffers (bases all 0) if the entity has none yet.</summary>
    public static void Ensure(EntityManager em, Entity entity)
    {
        if (!em.HasBuffer<StatBaseElement>(entity))
        {
            var bases = em.AddBuffer<StatBaseElement>(entity);
            for (int i = 0; i < (int)StatId.Count; i++) bases.Add(default);
        }
        if (!em.HasBuffer<StatModifier>(entity)) em.AddBuffer<StatModifier>(entity);
        if (!em.HasComponent<StatModifierExpiry>(entity)) em.AddComponent<StatModifierExpiry>(entity);
        if (!em.HasComponent<ModifiersDirty>(entity)) em.AddComponent<ModifiersDirty>(entity);
    }

    public static void SetBase(EntityManager em, Entity entity, StatId stat, float value)
    {
        var bases = em.GetBuffer<StatBaseElement>(entity);
        while (bases.Length < (int)StatId.Count) bases.Add(default);
        bases[(int)stat] = new StatBaseElement { value = value };
    }

    /// <summary>Removes every modifier of <paramref name="source"/>, so its owner can add the current set.</summary>
    public static void ClearSource(EntityManager em, Entity entity, ModifierSource source)
        => StatMath.RemoveSource(em.GetBuffer<StatModifier>(entity), source);

    public static void Add(EntityManager em, Entity entity, StatId stat, ModifierOp op, float value,
        ModifierSource source, int sourceId = 0, double expiresAt = 0)
        => em.GetBuffer<StatModifier>(entity).Add(new StatModifier
        {
            stat = stat, op = op, value = value, source = source, sourceId = sourceId, expiresAt = expiresAt,
        });

    /// <summary>A level curve multiplier m as a ratio modifier (m − 1); 1 adds nothing.</summary>
    public static void AddLevelMultiplier(EntityManager em, Entity entity, StatId stat, float multiplier)
    {
        if (multiplier != 1f) Add(em, entity, stat, ModifierOp.Ratio, multiplier - 1f, ModifierSource.Level);
    }

    public static void MarkDirty(EntityManager em, Entity entity) => em.SetComponentEnabled<ModifiersDirty>(entity, true);
}
