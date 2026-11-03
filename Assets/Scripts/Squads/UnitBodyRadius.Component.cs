using Unity.Entities;

/// <summary>
/// Body radius of one unit (Aprendizajes_Referencia.md L5-5; ref.: each actor has its own radius): from
/// <c>SquadData.bodyRadius</c>, 0 = the global <see cref="MovementTuningComponent.bodyRadius"/>. Contact, avoidance,
/// push and reach use the sum of both bodies of a pair, so a larger unit (cavalry) keeps its distance. Bodies without
/// it (heroes) use the global radius.
/// </summary>
public struct UnitBodyRadiusComponent : IComponentData
{
    public float radius;
}

/// <summary>Resolves a unit's body radius (L5-5).</summary>
public static class BodyRadius
{
    public static float Resolve(float declared, float fallback) => declared > 0f ? declared : fallback;

    public static float Of(in ComponentLookup<UnitBodyRadiusComponent> lookup, Entity entity, float fallback)
        => lookup.TryGetComponent(entity, out var body) ? Resolve(body.radius, fallback) : fallback;
}
