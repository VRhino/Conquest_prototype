using NUnit.Framework;
using Unity.Entities;

/// <summary>Absorb shield between the damage calculation and health (Aprendizajes_Referencia.md L1-06).</summary>
public class AbsorbShieldTests
{
    [Test]
    public void AbsorbsFirstPassesTheExcessAndIgnoresZeroHitsAndExpiry()
    {
        var shield = new AbsorbShieldComponent { value = 20f };
        Assert.That(AbsorbShield.Absorb(ref shield, 0f, 0), Is.EqualTo(0f));
        Assert.That(shield.value, Is.EqualTo(20f), "A hit of 0 does not consume it.");
        Assert.That(AbsorbShield.Absorb(ref shield, 5f, 0), Is.EqualTo(0f));
        Assert.That(shield.value, Is.EqualTo(15f));
        Assert.That(AbsorbShield.Absorb(ref shield, 25f, 0), Is.EqualTo(10f), "The excess passes to health.");
        Assert.That(shield.value, Is.EqualTo(0f));

        var timed = new AbsorbShieldComponent { value = 50f, expiresAt = 3.0 };
        Assert.That(AbsorbShield.Absorb(ref timed, 10f, 3.0), Is.EqualTo(10f), "Expired: absorbs nothing.");
        Assert.That(timed.value, Is.EqualTo(50f));
    }

    [Test]
    public void DamageCalculationTakesHitsFromTheShieldBeforeHealth()
    {
        using var world = new World("Absorb shield");
        var em = world.EntityManager;
        var unit = em.CreateEntity(typeof(HealthComponent), typeof(AbsorbShieldComponent));
        em.SetComponentData(unit, new HealthComponent { maxHealth = 100f, currentHealth = 100f });
        em.SetComponentData(unit, new AbsorbShieldComponent { value = 20f });
        var damage = world.GetOrCreateSystemManaged<DamageCalculationSystem>();

        Hit(em, unit, 5f);
        damage.Update();
        Assert.That(em.GetComponentData<HealthComponent>(unit).currentHealth, Is.EqualTo(100f));
        Assert.That(em.GetComponentData<AbsorbShieldComponent>(unit).value, Is.EqualTo(15f));

        Hit(em, unit, 25f);
        damage.Update();
        Assert.That(em.GetComponentData<HealthComponent>(unit).currentHealth, Is.EqualTo(90f));
        Assert.That(em.HasComponent<AbsorbShieldComponent>(unit), Is.False, "A depleted shield is removed.");
    }

    static void Hit(EntityManager em, Entity target, float blunt)
    {
        var hit = em.CreateEntity(typeof(PendingDamageEvent));
        em.SetComponentData(hit, new PendingDamageEvent
        {
            target = target, hasInlineDamageProfile = true, inlineDamageProfile = new DamageProfileComponent { bluntDamage = blunt },
            multiplier = 1f, destroyEntityAfterProcessing = true,
        });
    }
}
