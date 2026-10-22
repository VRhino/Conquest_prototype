using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Applies the hero's on-hit perk effects (Aprendizajes_Referencia.md L7-4): for each pending hit whose source carries
/// <see cref="PerkTrigger.OnHit"/> effects whose condition holds, a timed modifier goes on the hit target (source
/// <see cref="ModifierSource.Perk"/>). It reads the hits before <see cref="DamageCalculationSystem"/> consumes them.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(MeleeContactResolutionSystem))]
[UpdateBefore(typeof(DamageCalculationSystem))]
public partial class PerkOnHitSystem : SystemBase
{
    EntityQuery _hits;

    protected override void OnCreate()
    {
        _hits = GetEntityQuery(ComponentType.ReadOnly<PendingDamageEvent>());
        RequireForUpdate(_hits);
    }

    protected override void OnUpdate()
    {
        double now = SystemAPI.Time.ElapsedTime;
        using var hits = _hits.ToComponentDataArray<PendingDamageEvent>(Allocator.Temp);
        foreach (var hit in hits)
        {
            var source = hit.damageSource;
            if (!SystemAPI.Exists(source) || !SystemAPI.HasBuffer<HeroPerkElement>(source)) continue;
            if (!SystemAPI.Exists(hit.target) || !SystemAPI.HasBuffer<StatModifier>(hit.target)) continue;
            var motion = SystemAPI.HasComponent<HeroMotionComponent>(source) ? SystemAPI.GetComponent<HeroMotionComponent>(source) : default;
            bool applied = false;
            foreach (var perk in SystemAPI.GetBuffer<HeroPerkElement>(source))
            {
                var e = perk.effect;
                if (e.trigger != PerkTrigger.OnHit || e.target != PerkTarget.HitTarget || !PerkConditions.Met(e, motion)) continue;
                StatEffects.ApplyTimed(SystemAPI.GetBuffer<StatModifier>(hit.target), e.stat, e.op, e.value, now + e.duration,
                    ModifierSource.Perk, perk.perkId);
                applied = true;
            }
            if (applied && SystemAPI.HasComponent<ModifiersDirty>(hit.target))
                SystemAPI.SetComponentEnabled<ModifiersDirty>(hit.target, true);
        }
    }
}
