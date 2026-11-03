using NUnit.Framework;
using Unity.Entities;

/// <summary>Life steal as a stat of the modifier stack (Aprendizajes_Referencia.md L1-13).</summary>
public class LifeStealTests
{
    [Test]
    public void HealsAFractionOfTheHealthTakenUpToTheMaximum()
    {
        Assert.That(LifeSteal.Heal(50f, 100f, 40f, 0.25f), Is.EqualTo(60f).Within(1e-4f));
        Assert.That(LifeSteal.Heal(95f, 100f, 40f, 0.25f), Is.EqualTo(100f), "Never above the maximum.");
        Assert.That(LifeSteal.Heal(50f, 100f, 0f, 0.25f), Is.EqualTo(50f), "Nothing taken, nothing healed.");
        Assert.That(LifeSteal.Heal(50f, 100f, 40f, 0f), Is.EqualTo(50f));
    }

    [Test]
    public void TheAttackerHealsOnlyWhatTheHitReallyTook()
    {
        using var world = new World("Life steal");
        var em = world.EntityManager;
        var attacker = em.CreateEntity(typeof(HealthComponent));
        em.SetComponentData(attacker, new HealthComponent { maxHealth = 100f, currentHealth = 50f });
        StatStack.Ensure(em, attacker);
        em.AddComponent<CombatStatsComponent>(attacker);
        StatStack.SetBase(em, attacker, StatId.MaxHealth, 100f);
        StatStack.Add(em, attacker, StatId.LifeSteal, ModifierOp.Flat, 0.5f, ModifierSource.Perk);
        world.GetOrCreateSystemManaged<StatResolveSystem>().Update();
        Assert.That(em.GetComponentData<CombatStatsComponent>(attacker).lifeSteal, Is.EqualTo(0.5f).Within(1e-5f));

        var target = em.CreateEntity(typeof(HealthComponent));
        em.SetComponentData(target, new HealthComponent { maxHealth = 100f, currentHealth = 10f });
        var hit = em.CreateEntity(typeof(PendingDamageEvent));
        em.SetComponentData(hit, new PendingDamageEvent
        {
            target = target, damageSource = attacker, hasInlineDamageProfile = true,
            inlineDamageProfile = new DamageProfileComponent { bluntDamage = 40f },
            multiplier = 1f, destroyEntityAfterProcessing = true,
        });
        world.GetOrCreateSystemManaged<DamageCalculationSystem>().Update();
        Assert.That(em.GetComponentData<HealthComponent>(target).currentHealth, Is.EqualTo(0f));
        Assert.That(em.GetComponentData<HealthComponent>(attacker).currentHealth, Is.EqualTo(55f).Within(1e-4f),
            "Heals half of the 10 health the hit took, not of its 40 damage.");
    }
}
