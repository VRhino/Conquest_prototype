using NUnit.Framework;
using Unity.Entities;

/// <summary>
/// Shield-break stun as a speed override on the stack that expires (Aprendizajes_Referencia.md L5-3): damage adds it,
/// the resolver writes speed 0 for its duration and restores the speed on its own.
/// </summary>
public class StunSpeedTests
{
    const float StunSeconds = 2f;

    [Test]
    public void ShieldBreakImmobilisesThroughTheStackUntilTheStunExpires()
    {
        using var world = new World("Stun");
        var em = world.EntityManager;
        var unit = em.CreateEntity(typeof(HealthComponent), typeof(UnitStatsComponent), typeof(UnitShieldComponent));
        em.SetComponentData(unit, new HealthComponent { maxHealth = 100f, currentHealth = 100f });
        em.SetComponentData(unit, new UnitShieldComponent { currentBlock = 1f, maxBlock = 50f, breakStunDuration = StunSeconds });
        StatStack.Ensure(em, unit);
        StatStack.SetBase(em, unit, StatId.MaxHealth, 100f);
        StatStack.SetBase(em, unit, StatId.MoveSpeed, 4f);
        StatStack.MarkDirty(em, unit);
        var resolve = world.GetOrCreateSystemManaged<StatResolveSystem>();
        world.SetTime(new Unity.Core.TimeData(10.0, 0.02f));
        resolve.Update();
        Assert.That(em.GetComponentData<UnitStatsComponent>(unit).speed, Is.EqualTo(4f));

        var hit = em.CreateEntity(typeof(PendingDamageEvent));
        em.SetComponentData(hit, new PendingDamageEvent
        {
            target = unit, hasInlineDamageProfile = true, inlineDamageProfile = new DamageProfileComponent { bluntDamage = 30f },
            multiplier = 1f, hitType = HitType.Shield, destroyEntityAfterProcessing = true,
        });
        world.GetOrCreateSystemManaged<DamageCalculationSystem>().Update();
        resolve.Update();
        Assert.That(em.GetComponentData<UnitShieldComponent>(unit).brokenTimer, Is.EqualTo(StunSeconds));
        Assert.That(em.GetComponentData<UnitStatsComponent>(unit).speed, Is.EqualTo(0f), "Stunned: speed 0 from the stack.");

        world.SetTime(new Unity.Core.TimeData(10.0 + StunSeconds - 0.1, 0.02f));
        resolve.Update();
        Assert.That(em.GetComponentData<UnitStatsComponent>(unit).speed, Is.EqualTo(0f));

        world.SetTime(new Unity.Core.TimeData(10.0 + StunSeconds + 0.01, 0.02f));
        resolve.Update();
        Assert.That(em.GetComponentData<UnitStatsComponent>(unit).speed, Is.EqualTo(4f), "The override expires on its own.");
        Assert.That(em.GetBuffer<StatModifier>(unit).Length, Is.EqualTo(0));
    }

    [Test]
    public void HeroWalkingSpeedComesFromTheStack()
    {
        using var world = new World("Hero speed");
        var em = world.EntityManager;
        var hero = em.CreateEntity(typeof(MoveSpeedComponent));
        StatStack.Ensure(em, hero);
        StatStack.SetBase(em, hero, StatId.MoveSpeed, 5f);
        var modifiers = em.GetBuffer<StatModifier>(hero);
        StatEffects.Immobilize(modifiers, 3.0, StatEffects.ShieldBreakStun);
        StatEffects.Immobilize(em.GetBuffer<StatModifier>(hero), 4.0, StatEffects.ShieldBreakStun);
        Assert.That(em.GetBuffer<StatModifier>(hero).Length, Is.EqualTo(1), "A new stun replaces the previous one.");
        StatStack.MarkDirty(em, hero);
        var resolve = world.GetOrCreateSystemManaged<StatResolveSystem>();

        world.SetTime(new Unity.Core.TimeData(3.5, 0.02f));
        resolve.Update();
        Assert.That(em.GetComponentData<MoveSpeedComponent>(hero).value, Is.EqualTo(0f));
        world.SetTime(new Unity.Core.TimeData(4.5, 0.02f));
        resolve.Update();
        Assert.That(em.GetComponentData<MoveSpeedComponent>(hero).value, Is.EqualTo(5f));
    }
}
