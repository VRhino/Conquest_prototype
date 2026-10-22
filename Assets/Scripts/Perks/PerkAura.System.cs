using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Applies the hero's passive perk auras (Aprendizajes_Referencia.md L7-4; ref.: perks are effects with conditions, not
/// logic of their own): while an <see cref="PerkTrigger.Always"/> effect's condition holds, it refreshes a short-lived
/// modifier (source <see cref="ModifierSource.Perk"/>) on its targets, so the effect follows squad changes and ends on its
/// own when the condition stops holding.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(HeroMotionSystem))]
public partial class PerkAuraSystem : SystemBase
{
    /// <summary>Seconds between refreshes; each refresh lasts two of them.</summary>
    const float RefreshInterval = 0.25f;

    EntityQuery _bodies;

    protected override void OnCreate()
    {
        _bodies = GetEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<TeamComponent>(), ComponentType.ReadWrite<StatModifier>() },
            None = new[] { ComponentType.ReadOnly<IsDeadComponent>() },
        });
    }

    protected override void OnUpdate()
    {
        double now = SystemAPI.Time.ElapsedTime;
        using var heroes = SystemAPI.QueryBuilder().WithAll<HeroPerkElement, HeroMotionComponent>().Build().ToEntityArray(Allocator.Temp);
        var targets = new NativeList<Entity>(32, Allocator.Temp);
        foreach (var hero in heroes)
        {
            if (SystemAPI.HasComponent<HeroLifeComponent>(hero) && !SystemAPI.GetComponent<HeroLifeComponent>(hero).isAlive) continue;
            var motion = SystemAPI.GetComponent<HeroMotionComponent>(hero);
            var perks = SystemAPI.GetBuffer<HeroPerkElement>(hero);
            for (int i = 0; i < perks.Length; i++)
            {
                var perk = perks[i];
                if (perk.effect.trigger != PerkTrigger.Always || now < perk.nextRefresh) continue;
                if (!PerkConditions.Met(perk.effect, motion)) continue;
                perk.nextRefresh = now + RefreshInterval;
                perks[i] = perk;

                targets.Clear();
                SelectTargets(hero, perk.effect, targets);
                foreach (var target in targets) Apply(target, perk, now + 2 * RefreshInterval);
            }
        }
        targets.Dispose();
    }

    void SelectTargets(Entity hero, in PerkEffect effect, NativeList<Entity> targets)
    {
        switch (effect.target)
        {
            case PerkTarget.Self:
                targets.Add(hero);
                break;
            case PerkTarget.OwnSquad:
                if (!SystemAPI.HasComponent<HeroSquadReference>(hero)) break;
                var squad = SystemAPI.GetComponent<HeroSquadReference>(hero).squad;
                if (!SystemAPI.Exists(squad) || !SystemAPI.HasBuffer<SquadUnitElement>(squad)) break;
                foreach (var unit in SystemAPI.GetBuffer<SquadUnitElement>(squad))
                    if (SystemAPI.Exists(unit.Value)) targets.Add(unit.Value);
                break;
            case PerkTarget.AlliesInRadius:
                if (!SystemAPI.HasComponent<LocalTransform>(hero) || !SystemAPI.HasComponent<TeamComponent>(hero)) break;
                float3 centre = SystemAPI.GetComponent<LocalTransform>(hero).Position;
                var team = SystemAPI.GetComponent<TeamComponent>(hero).value;
                using (var bodies = _bodies.ToEntityArray(Allocator.Temp))
                using (var poses = _bodies.ToComponentDataArray<LocalTransform>(Allocator.Temp))
                using (var teams = _bodies.ToComponentDataArray<TeamComponent>(Allocator.Temp))
                {
                    for (int i = 0; i < bodies.Length; i++)
                        if (teams[i].value == team && math.distancesq(poses[i].Position.xz, centre.xz) <= effect.radius * effect.radius)
                            targets.Add(bodies[i]);
                }
                if (!targets.Contains(hero)) targets.Add(hero);
                break;
        }
    }

    void Apply(Entity target, in HeroPerkElement perk, double until)
    {
        if (!SystemAPI.HasBuffer<StatModifier>(target)) return;
        var e = perk.effect;
        StatEffects.ApplyTimed(SystemAPI.GetBuffer<StatModifier>(target), e.stat, e.op, e.value, until, ModifierSource.Perk, perk.perkId);
        if (SystemAPI.HasComponent<ModifiersDirty>(target)) SystemAPI.SetComponentEnabled<ModifiersDirty>(target, true);
    }
}
