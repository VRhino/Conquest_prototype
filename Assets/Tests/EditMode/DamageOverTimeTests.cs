using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>Damage over time with its own period and duration (Aprendizajes_Referencia.md L2-11).</summary>
public class DamageOverTimeTests
{
    [Test]
    public void TicksComeOncePerPeriodUntilTheEnd()
    {
        var d = new DamageOverTimeElement { period = 1f, nextTick = 1.0, endsAt = 2.0 };
        Assert.That(DamageOverTime.DueTicks(ref d, 0.5), Is.Zero);
        Assert.That(DamageOverTime.DueTicks(ref d, 1.0), Is.EqualTo(1));
        Assert.That(DamageOverTime.DueTicks(ref d, 5.0), Is.EqualTo(1), "Only the tick at 2 s: none after the end.");
        Assert.That(d.nextTick, Is.GreaterThan(d.endsAt));
    }

    [Test]
    public void ARecastRefreshesOrStacksTheSameBurn()
    {
        using var world = new World("Burn apply");
        var em = world.EntityManager;
        var caster = em.CreateEntity();
        var buffer = em.AddBuffer<DamageOverTimeElement>(em.CreateEntity());
        var burn = new DamageOverTimeElement { caster = caster, skillId = 4, multiplier = 0.3f, period = 1f, endsAt = 3.0 };
        DamageOverTime.Apply(buffer, burn, 1, EffectReapply.Refresh, 0.0);
        Assert.That(buffer[0].nextTick, Is.EqualTo(1.0), "First tick one period later.");
        burn.endsAt = 4.5;
        DamageOverTime.Apply(buffer, burn, 3, EffectReapply.Stack, 0.5);
        Assert.That(buffer.Length, Is.EqualTo(1));
        Assert.That(buffer[0].stacks, Is.EqualTo(2));
        Assert.That(buffer[0].endsAt, Is.EqualTo(4.5));
        Assert.That(buffer[0].nextTick, Is.EqualTo(1.0), "The tick rhythm is kept.");
    }

    [Test]
    public void ASkillBurnsTheEnemiesInTheAreaAndTheTicksHitThroughTheDamageSystem()
    {
        using var world = new World("Burn");
        var em = world.EntityManager;
        var caster = em.CreateEntity(typeof(DamageProfileComponent));
        em.SetComponentData(caster, new DamageProfileComponent { bluntDamage = 10f });
        var enemy = em.CreateEntity(typeof(LocalTransform), typeof(TeamComponent), typeof(HealthComponent));
        em.SetComponentData(enemy, LocalTransform.FromPosition(new float3(0f, 0f, 5f)));
        em.SetComponentData(enemy, new TeamComponent { value = Team.TeamB });
        em.SetComponentData(enemy, new HealthComponent { maxHealth = 100f, currentHealth = 100f });
        var request = em.CreateEntity();
        em.AddComponentData(request, new SkillEffectRequestComponent
        {
            caster = caster, skillId = 8, team = Team.TeamA, forward = new float3(0, 0, 1),
            effect = new SkillEvent
            {
                type = SkillEventType.DamageOverTime, radius = 3f, distance = 5f, value = 2f, period = 1f, duration = 2f,
            },
        });
        world.SetTime(new TimeData(0.0, 0.02f));
        world.GetOrCreateSystemManaged<SkillEffectSystem>().Update();
        Assert.That(em.GetBuffer<DamageOverTimeElement>(enemy).Length, Is.EqualTo(1));

        var burns = world.GetOrCreateSystemManaged<DamageOverTimeSystem>();
        var damage = world.GetOrCreateSystemManaged<DamageCalculationSystem>();
        foreach (double t in new[] { 0.5, 1.0, 2.0, 3.0 })
        {
            world.SetTime(new TimeData(t, 0.02f));
            burns.Update();
            damage.Update();
        }
        Assert.That(em.GetComponentData<HealthComponent>(enemy).currentHealth, Is.EqualTo(60f).Within(1e-3f),
            "Two ticks of 10 × 2.");
        Assert.That(em.GetBuffer<DamageOverTimeElement>(enemy).Length, Is.Zero, "Dropped once it ended.");
    }
}
