using NUnit.Framework;
using Unity.Entities;

/// <summary>Health floor and respawn protection (Aprendizajes_Referencia.md L1-07).</summary>
public class HealthFloorTests
{
    [Test]
    public void AHitNeverTakesHealthBelowTheFloorNorHealsAboveIt()
    {
        Assert.That(HealthFloor.Apply(100f, 30f, 100f, 0f), Is.EqualTo(70f), "No floor: plain subtraction.");
        Assert.That(HealthFloor.Apply(100f, 500f, 100f, 0f), Is.EqualTo(0f));
        Assert.That(HealthFloor.Apply(100f, 80f, 100f, 0.5f), Is.EqualTo(50f), "Stops at the floor.");
        Assert.That(HealthFloor.Apply(40f, 10f, 100f, 0.5f), Is.EqualTo(40f), "Below the floor already: no loss, no heal.");
        Assert.That(HealthFloor.Apply(100f, 500f, 100f, 2f), Is.EqualTo(100f), "The ratio is clamped to 1.");
    }

    [Test]
    public void TheResolverWritesTheFloorAndDamageRespectsIt()
    {
        using var world = new World("Health floor");
        var em = world.EntityManager;
        var hero = em.CreateEntity(typeof(HeroHealthComponent));
        em.SetComponentData(hero, new HeroHealthComponent { maxHealth = 100f, currentHealth = 100f });
        StatStack.Ensure(em, hero);
        StatStack.SetBase(em, hero, StatId.MaxHealth, 100f);
        StatEffects.ApplyTimed(em.GetBuffer<StatModifier>(hero), StatId.MinHealthRatio, ModifierOp.Flat, 0.3f, 0,
            ModifierSource.Effect, StatEffects.RespawnProtection);
        world.GetOrCreateSystemManaged<StatResolveSystem>().Update();
        Assert.That(em.GetComponentData<HeroHealthComponent>(hero).minHealthRatio, Is.EqualTo(0.3f).Within(1e-5f));

        var hit = em.CreateEntity(typeof(PendingDamageEvent));
        em.SetComponentData(hit, new PendingDamageEvent
        {
            target = hero, hasInlineDamageProfile = true, inlineDamageProfile = new DamageProfileComponent { bluntDamage = 500f },
            multiplier = 1f, destroyEntityAfterProcessing = true,
        });
        world.GetOrCreateSystemManaged<DamageCalculationSystem>().Update();
        Assert.That(em.GetComponentData<HeroHealthComponent>(hero).currentHealth, Is.EqualTo(30f).Within(1e-4f));
        Assert.That(em.HasComponent<IsDeadComponent>(hero), Is.False);
    }

    [Test]
    public void RespawnGivesATimedFloorFromTheConfig()
    {
        using var world = new World("Respawn protection");
        var em = world.EntityManager;
        em.CreateEntity(typeof(MatchStateComponent));
        var config = em.CreateEntity(typeof(HeroGameplayConfigComponent));
        em.SetComponentData(config, new HeroGameplayConfigComponent { respawnProtectionSeconds = 5f, respawnProtectionHealthRatio = 0.3f });
        var hero = em.CreateEntity(typeof(HeroLifeComponent), typeof(HeroHealthComponent), typeof(HeroSpawnComponent), typeof(IsLocalPlayer));
        em.SetComponentData(hero, new HeroLifeComponent { isAlive = false, deathTimer = 0f });
        em.SetComponentData(hero, new HeroHealthComponent { maxHealth = 100f });
        StatStack.Ensure(em, hero);
        em.SetComponentEnabled<ModifiersDirty>(hero, false);

        world.GetOrCreateSystemManaged<HeroRespawnSystem>().Update();

        Assert.That(em.GetComponentData<HeroLifeComponent>(hero).isAlive, Is.True);
        var modifiers = em.GetBuffer<StatModifier>(hero);
        Assert.That(modifiers.Length, Is.EqualTo(1));
        Assert.That(modifiers[0].stat, Is.EqualTo(StatId.MinHealthRatio));
        Assert.That(modifiers[0].value, Is.EqualTo(0.3f).Within(1e-5f));
        Assert.That(modifiers[0].expiresAt, Is.EqualTo(5.0).Within(1e-6), "Expires after the protection window.");
        Assert.That(em.IsComponentEnabled<ModifiersDirty>(hero), Is.True);
    }
}
