using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>Most targets per skill and effects bound to the skill (Aprendizajes_Referencia.md L2-13, L2-14).</summary>
public class SkillTargetsAndBindingTests
{
    [Test]
    public void EveryEffectEventReachesTheEffectSystem()
    {
        foreach (var type in new[] { SkillEventType.AreaDamage, SkillEventType.ApplyModifier, SkillEventType.AbsorbShield,
                     SkillEventType.ApplyConditional, SkillEventType.Taunt })
        {
            Assert.That(SkillEvents.IsEffect(type, false), Is.True, type + " from a hero.");
            Assert.That(SkillEvents.IsEffect(type, true), Is.True, type + " from a squad.");
        }
        Assert.That(SkillEvents.IsEffect(SkillEventType.Dash, false), Is.True);
        Assert.That(SkillEvents.IsEffect(SkillEventType.Dash, true), Is.False, "A squad has no body to dash with.");
        Assert.That(SkillEvents.IsEffect(SkillEventType.HitboxOpen, false), Is.False, "Weapon events stay with the skill.");
    }

    [Test]
    public void OnlyTheNearestTargetsAreKept()
    {
        using var world = new World("Nearest");
        var em = world.EntityManager;
        var targets = new NativeList<Entity>(Allocator.Temp);
        var positions = new NativeList<float3>(Allocator.Temp);
        float[] distances = { 5f, 1f, 3f, 2f };
        foreach (float d in distances)
        {
            targets.Add(em.CreateEntity());
            positions.Add(new float3(d, 0f, 0f));
        }
        SkillEvents.KeepNearest(targets, positions, float3.zero, 2);
        Assert.That(targets.Length, Is.EqualTo(2));
        Assert.That(positions[0].x, Is.EqualTo(1f));
        Assert.That(positions[1].x, Is.EqualTo(2f));
        targets.Dispose();
        positions.Dispose();
    }

    [Test]
    public void TheCasterModifiersChangeTheCapButNotAnUncappedEvent()
    {
        var mods = new NativeArray<StatModifier>(2, Allocator.Temp);
        mods[0] = new StatModifier { stat = StatId.MaxTargets, op = ModifierOp.Ratio, value = 0.5f, source = ModifierSource.Perk };
        mods[1] = new StatModifier { stat = StatId.MaxTargets, op = ModifierOp.Flat, value = 1f, source = ModifierSource.Effect };
        Assert.That(SkillEvents.Cap(4, mods, 0), Is.EqualTo(7), "4 × 1.5 + 1.");
        Assert.That(SkillEvents.Cap(0, mods, 0), Is.Zero, "No cap stays no cap.");
        mods[0] = new StatModifier { stat = StatId.MaxTargets, op = ModifierOp.Ratio, value = -0.9f, source = ModifierSource.Effect };
        mods[1] = default;
        Assert.That(SkillEvents.Cap(4, mods, 0), Is.EqualTo(1), "A cap never drops below one target.");
        mods.Dispose();
    }

    [Test]
    public void AnAreaWithACapHitsOnlyThatMany() => CappedArea(0f, 2);

    [Test]
    public void AModifierOnTheCasterRaisesTheCap() => CappedArea(1f, 3);

    static void CappedArea(float extraTargets, int expected)
    {
        using var world = new World("Cap");
        var em = world.EntityManager;
        var hit = new Entity[4];
        for (int i = 0; i < hit.Length; i++)
        {
            hit[i] = em.CreateEntity(typeof(LocalTransform), typeof(TeamComponent), typeof(HealthComponent));
            em.SetComponentData(hit[i], LocalTransform.FromPosition(new float3(0f, 0f, 1f + i)));
            em.SetComponentData(hit[i], new TeamComponent { value = Team.TeamB });
            StatStack.Ensure(em, hit[i]);
        }
        var caster = em.CreateEntity();
        StatStack.Ensure(em, caster);
        if (extraTargets != 0f) StatStack.Add(em, caster, StatId.MaxTargets, ModifierOp.Flat, extraTargets, ModifierSource.Perk);
        var request = em.CreateEntity();
        em.AddComponentData(request, new SkillEffectRequestComponent
        {
            caster = caster, skillId = 3, team = Team.TeamA, forward = new float3(0, 0, 1),
            effect = new SkillEvent
            {
                type = SkillEventType.ApplyModifier, target = SkillTarget.EnemiesInRadius, radius = 10f, maxTargets = 2,
                stat = StatId.MoveSpeed, op = ModifierOp.Ratio, value = -0.3f, duration = 2f,
            },
        });
        world.GetOrCreateSystemManaged<SkillEffectSystem>().Update();
        for (int i = 0; i < hit.Length; i++)
            Assert.That(em.GetBuffer<StatModifier>(hit[i]).Length, Is.EqualTo(i < expected ? 1 : 0),
                i < expected ? "One of the nearest." : "Beyond the cap.");
    }

    [Test]
    public void ABoundModifierGoesWhenItsInstanceStops()
    {
        using var world = new World("Binding");
        var em = world.EntityManager;
        var caster = em.CreateEntity();
        em.AddComponentData(caster, new SkillInstanceComponent { active = true, instanceId = 7 });
        var target = em.CreateEntity(typeof(LocalTransform), typeof(TeamComponent), typeof(HealthComponent));
        em.SetComponentData(target, new TeamComponent { value = Team.TeamB });
        StatStack.Ensure(em, target);
        var request = em.CreateEntity();
        em.AddComponentData(request, new SkillEffectRequestComponent
        {
            caster = caster, skillId = 3, instanceId = 7, team = Team.TeamA, forward = new float3(0, 0, 1),
            effect = new SkillEvent
            {
                type = SkillEventType.ApplyModifier, target = SkillTarget.EnemiesInRadius, radius = 10f, boundToSkill = true,
                stat = StatId.MoveSpeed, op = ModifierOp.Ratio, value = -0.3f, duration = 30f,
            },
        });
        world.GetOrCreateSystemManaged<SkillEffectSystem>().Update();
        var binding = world.GetOrCreateSystemManaged<SkillBindingSystem>();
        binding.Update();
        Assert.That(em.GetBuffer<StatModifier>(target).Length, Is.EqualTo(1), "Kept while the instance runs.");

        em.SetComponentData(caster, new SkillInstanceComponent { active = false, instanceId = 7 });
        binding.Update();
        Assert.That(em.GetBuffer<StatModifier>(target).Length, Is.Zero, "Gone when the skill ends, well before its 30 s.");
        Assert.That(em.IsComponentEnabled<BoundModifiersTag>(target), Is.False);
    }
}
