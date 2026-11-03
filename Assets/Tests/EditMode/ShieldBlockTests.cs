using NUnit.Framework;
using Unity.Entities;

/// <summary>Hits that ignore the shield and block wear per weapon (Aprendizajes_Referencia.md L1-08, L1-10).</summary>
public class ShieldBlockTests
{
    static readonly UnitShieldComponent Ready = new UnitShieldComponent { currentBlock = 50f, maxBlock = 50f };

    [Test]
    public void OnlyAShieldContactWithBlockLeftStopsAHitThatDoesNotIgnoreIt()
    {
        var plain = new HitTraits();
        Assert.That(ShieldBlock.Blocks(Ready, HitType.Shield, plain), Is.True);
        Assert.That(ShieldBlock.Blocks(Ready, HitType.Body, plain), Is.False);
        Assert.That(ShieldBlock.Blocks(Ready, HitType.Shield, new HitTraits { ignoresShield = true }), Is.False);
        var broken = Ready;
        broken.brokenTimer = 1f;
        Assert.That(ShieldBlock.Blocks(broken, HitType.Shield, plain), Is.False);
        var empty = Ready;
        empty.currentBlock = 0f;
        Assert.That(ShieldBlock.Blocks(empty, HitType.Shield, plain), Is.False);
    }

    [Test]
    public void BlockAndStaminaDamageScaleWithTheWeapon()
    {
        Assert.That(ShieldBlock.BlockDamage(20f, new HitTraits()), Is.EqualTo(20f), "Neutral: the block takes the hit.");
        Assert.That(ShieldBlock.BlockDamage(20f, new HitTraits { blockDamageRatio = 0.5f }), Is.EqualTo(30f).Within(1e-4f));
        Assert.That(ShieldBlock.BlockDamage(20f, new HitTraits { blockDamageRatio = -2f }), Is.EqualTo(0f), "Never negative.");
        Assert.That(ShieldBlock.StaminaDamage(20f, new HitTraits()), Is.EqualTo(0f));
        Assert.That(ShieldBlock.StaminaDamage(20f, new HitTraits { staminaDamageRatio = 0.25f }), Is.EqualTo(5f).Within(1e-4f));
    }

    [Test]
    public void DamageCalculationWearsTheBlockAndTheHeroStaminaOrGoesThrough()
    {
        using var world = new World("Shield block");
        var em = world.EntityManager;
        var hero = em.CreateEntity(typeof(HeroHealthComponent), typeof(UnitShieldComponent), typeof(StaminaComponent));
        em.SetComponentData(hero, new HeroHealthComponent { maxHealth = 100f, currentHealth = 100f });
        em.SetComponentData(hero, Ready);
        em.SetComponentData(hero, new StaminaComponent { maxStamina = 100f, currentStamina = 100f });
        var damage = world.GetOrCreateSystemManaged<DamageCalculationSystem>();

        Hit(em, hero, 10f, new HitTraits { blockDamageRatio = 1f, staminaDamageRatio = 0.5f });
        damage.Update();
        Assert.That(em.GetComponentData<UnitShieldComponent>(hero).currentBlock, Is.EqualTo(30f).Within(1e-4f),
            "A weapon with block ratio 1 wears the block twice as fast.");
        Assert.That(em.GetComponentData<StaminaComponent>(hero).currentStamina, Is.EqualTo(95f).Within(1e-4f));
        Assert.That(em.GetComponentData<HeroHealthComponent>(hero).currentHealth, Is.EqualTo(100f), "Blocked: no health lost.");

        Hit(em, hero, 10f, new HitTraits { ignoresShield = true });
        damage.Update();
        Assert.That(em.GetComponentData<HeroHealthComponent>(hero).currentHealth, Is.EqualTo(90f).Within(1e-4f),
            "A hit that ignores the shield reaches health.");
        Assert.That(em.GetComponentData<UnitShieldComponent>(hero).currentBlock, Is.EqualTo(30f).Within(1e-4f),
            "...and leaves the block untouched.");
    }

    static void Hit(EntityManager em, Entity target, float blunt, HitTraits traits)
    {
        var hit = em.CreateEntity(typeof(PendingDamageEvent));
        em.SetComponentData(hit, new PendingDamageEvent
        {
            target = target, hasInlineDamageProfile = true,
            inlineDamageProfile = new DamageProfileComponent { bluntDamage = blunt, traits = traits },
            multiplier = 1f, destroyEntityAfterProcessing = true, hitType = HitType.Shield,
        });
    }
}
