using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>Squad AI parameters as stats of the squad's modifier stack (Aprendizajes_Referencia.md L4-10).</summary>
public class SquadAIStatsTests
{
    [Test]
    public void TheResolverWritesTheSquadAIParametersFromItsStack()
    {
        using var world = new World("Squad AI stats");
        var em = world.EntityManager;
        var squad = em.CreateEntity();
        UnitStatsUtility.SeedSquadAI(squad, em);
        world.GetOrCreateSystemManaged<StatResolveSystem>().Update();
        var neutral = em.GetComponentData<SquadAIStatsComponent>(squad);
        Assert.That(neutral.detectionRangeScale, Is.EqualTo(1f));
        Assert.That(neutral.combatRadiusScale, Is.EqualTo(1f));
        Assert.That(neutral.heroBias, Is.EqualTo(0f));

        StatStack.Add(em, squad, StatId.AIDetectionRange, ModifierOp.Ratio, 0.5f, ModifierSource.Effect);
        StatStack.Add(em, squad, StatId.AICombatRadius, ModifierOp.Ratio, -0.25f, ModifierSource.Effect);
        StatStack.Add(em, squad, StatId.AIHeroBias, ModifierOp.Flat, -0.3f, ModifierSource.Effect);
        StatStack.MarkDirty(em, squad);
        world.GetOrCreateSystemManaged<StatResolveSystem>().Update();
        var stats = em.GetComponentData<SquadAIStatsComponent>(squad);
        Assert.That(stats.detectionRangeScale, Is.EqualTo(1.5f).Within(1e-5f));
        Assert.That(stats.combatRadiusScale, Is.EqualTo(0.75f).Within(1e-5f));
        Assert.That(stats.heroBias, Is.EqualTo(-0.3f).Within(1e-5f), "A bias may be negative.");
    }

    [Test]
    public void ASkillAimedAtSoldiersPutsTheAIModifierOnTheirSquadOnce()
    {
        using var world = new World("Squad AI effect");
        var em = world.EntityManager;
        var squad = em.CreateEntity();
        UnitStatsUtility.SeedSquadAI(squad, em);
        var units = em.AddBuffer<SquadUnitElement>(squad);
        for (int i = 0; i < 3; i++)
        {
            var unit = em.CreateEntity(typeof(LocalTransform), typeof(TeamComponent), typeof(HealthComponent));
            em.SetComponentData(unit, LocalTransform.FromPosition(new float3(i, 0, 2)));
            em.SetComponentData(unit, new TeamComponent { value = Team.TeamB });
            em.AddComponentData(unit, new UnitOwnerComponent { squad = squad });
            em.GetBuffer<SquadUnitElement>(squad).Add(new SquadUnitElement { Value = unit });
        }
        var caster = em.CreateEntity();
        var request = em.CreateEntity();
        em.AddComponentData(request, new SkillEffectRequestComponent
        {
            caster = caster, skillId = 42, team = Team.TeamA, origin = float3.zero, forward = new float3(0, 0, 1),
            effect = new SkillEvent
            {
                type = SkillEventType.ApplyModifier, target = SkillTarget.EnemiesInRadius, radius = 10f,
                stat = StatId.AIDetectionRange, op = ModifierOp.Ratio, value = -0.5f, duration = 5f,
            },
        });
        world.GetOrCreateSystemManaged<SkillEffectSystem>().Update();

        var modifiers = em.GetBuffer<StatModifier>(squad);
        Assert.That(modifiers.Length, Is.EqualTo(1), "Three soldiers hit, one modifier on their squad.");
        Assert.That(modifiers[0].stat, Is.EqualTo(StatId.AIDetectionRange));
        world.GetOrCreateSystemManaged<StatResolveSystem>().Update();
        Assert.That(em.GetComponentData<SquadAIStatsComponent>(squad).detectionRangeScale, Is.EqualTo(0.5f).Within(1e-5f));
    }
}
