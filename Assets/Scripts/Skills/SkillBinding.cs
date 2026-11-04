using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// On while an entity carries modifiers bound to a skill instance (Aprendizajes_Referencia.md L2-14), so
/// <see cref="SkillBindingSystem"/> only visits those.
/// </summary>
public struct BoundModifiersTag : IComponentData, IEnableableComponent { }

/// <summary>Which skill events are effects that <see cref="SkillEffectSystem"/> applies (the rest belong to the weapon).</summary>
public static class SkillEvents
{
    /// <param name="squadCaster">A squad has no body to dash with.</param>
    public static bool IsEffect(SkillEventType type, bool squadCaster) => type switch
    {
        SkillEventType.AreaDamage or SkillEventType.ApplyModifier or SkillEventType.AbsorbShield
            or SkillEventType.ApplyConditional or SkillEventType.Taunt or SkillEventType.DamageOverTime => true,
        SkillEventType.Dash => !squadCaster,
        _ => false,
    };

    /// <summary>
    /// The event's most targets after the caster's <see cref="StatId.MaxTargets"/> modifiers (ref.: a skill's most hits can
    /// be changed by effects): its own cap is the base, at least one target is kept, and 0 (no cap) stays 0.
    /// </summary>
    public static int Cap(int eventCap, NativeArray<StatModifier> casterModifiers, double now)
    {
        if (eventCap <= 0) return 0;
        return math.max(1, (int)math.round(StatMath.Resolve(eventCap, casterModifiers, StatId.MaxTargets, now)));
    }

    /// <summary>
    /// Keeps the <paramref name="max"/> targets nearest to <paramref name="centre"/> (L2-13; ref.: each skill carries a most
    /// hits count). <paramref name="max"/> ≤ 0 keeps them all.
    /// </summary>
    public static void KeepNearest(NativeList<Entity> targets, NativeList<float3> positions, float3 centre, int max)
    {
        if (max <= 0 || targets.Length <= max) return;
        // Selection by distance; N is a squad or two.
        for (int i = 0; i < max; i++)
        {
            int best = i;
            float bestSq = math.distancesq(positions[i].xz, centre.xz);
            for (int j = i + 1; j < targets.Length; j++)
            {
                float d = math.distancesq(positions[j].xz, centre.xz);
                if (d < bestSq) { bestSq = d; best = j; }
            }
            (targets[i], targets[best]) = (targets[best], targets[i]);
            (positions[i], positions[best]) = (positions[best], positions[i]);
        }
        targets.ResizeUninitialized(max);
        positions.ResizeUninitialized(max);
    }

    /// <summary>A modifier bound to (<paramref name="caster"/>, <paramref name="instance"/>) still applies while that instance runs.</summary>
    public static bool BindingAlive(bool casterExists, in SkillInstanceComponent current, bool parkedActive, uint parkedInstance,
        uint instance)
        => casterExists && ((current.active && current.instanceId == instance) || (parkedActive && parkedInstance == instance));
}

/// <summary>
/// Removes the modifiers bound to a skill instance once it ends or is interrupted (L2-14; ref.: on removing an instance,
/// the buffs linked to it are removed). Runs after <see cref="SkillExecutionSystem"/>; only touches bound modifiers.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(SkillExecutionSystem))]
public partial class SkillBindingSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var instances = GetComponentLookup<SkillInstanceComponent>(true);
        var parked = GetComponentLookup<ParkedSkillComponent>(true);
        foreach (var (modifiers, entity) in SystemAPI.Query<DynamicBuffer<StatModifier>>()
                     .WithAll<BoundModifiersTag>().WithEntityAccess())
        {
            int bound = 0, removed = 0;
            for (int i = modifiers.Length - 1; i >= 0; i--)
            {
                var m = modifiers[i];
                if (m.boundCaster == Entity.Null) continue;
                bool exists = SystemAPI.Exists(m.boundCaster);
                var current = exists && instances.HasComponent(m.boundCaster) ? instances[m.boundCaster] : default;
                bool parkedActive = exists && parked.HasComponent(m.boundCaster) && parked[m.boundCaster].active;
                uint parkedInstance = parkedActive ? parked[m.boundCaster].instance.instanceId : 0;
                if (SkillEvents.BindingAlive(exists, current, parkedActive, parkedInstance, m.boundInstance)) { bound++; continue; }
                modifiers.RemoveAt(i);
                removed++;
            }
            if (removed > 0 && SystemAPI.HasComponent<ModifiersDirty>(entity))
                SystemAPI.SetComponentEnabled<ModifiersDirty>(entity, true);
            if (bound == 0) SystemAPI.SetComponentEnabled<BoundModifiersTag>(entity, false);
        }
    }
}
