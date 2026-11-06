using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>The hero's combo and soldier kills, gained in battle and spent by skills (Aprendizajes_Referencia.md L2-16).</summary>
public class HeroResourcesTests
{
    static readonly ComboTuning Tuning = ComboTuning.Default;

    [Test]
    public void HitsAddComboUpToTheMaximumAndItDecaysAfterTheDelay()
    {
        var r = new HeroResourcesComponent();
        for (int i = 0; i < 5; i++) HeroResources.Landed(ref r, 1.0 + i * 0.1, Tuning);
        Assert.That(HeroResources.Combo(r, 1.4, Tuning), Is.EqualTo(5f * Tuning.gainPerHit));
        Assert.That(HeroResources.Combo(r, 1.4 + Tuning.decayDelay, Tuning), Is.EqualTo(5f), "Still within the delay.");
        Assert.That(HeroResources.Combo(r, 1.4 + Tuning.decayDelay + 0.5, Tuning),
            Is.EqualTo(5f - Tuning.decayPerSecond * 0.5f).Within(1e-4f));
        Assert.That(HeroResources.Combo(r, 100.0, Tuning), Is.EqualTo(0f), "Never below 0.");

        for (int i = 0; i < 100; i++) HeroResources.Landed(ref r, 200.0, Tuning);
        Assert.That(HeroResources.Combo(r, 200.0, Tuning), Is.EqualTo(Tuning.max));
    }

    [Test]
    public void PayingSpendsBothAndTheRestKeepsDecayingFromTheLastHit()
    {
        var r = new HeroResourcesComponent { combo = 25f, lastHitTime = 0.0, kills = 4 };
        double now = Tuning.decayDelay + 1.0; // Lost decayPerSecond already: 20 left.
        Assert.That(HeroResources.CanPay(r, now, Tuning, 20f, 3), Is.True);
        Assert.That(HeroResources.CanPay(r, now, Tuning, 21f, 0), Is.False);
        Assert.That(HeroResources.CanPay(r, now, Tuning, 0f, 5), Is.False);
        HeroResources.Pay(ref r, now, Tuning, 15f, 3);
        Assert.That(HeroResources.Combo(r, now, Tuning), Is.EqualTo(5f).Within(1e-4f));
        Assert.That(HeroResources.Combo(r, now + 0.5, Tuning), Is.EqualTo(5f - Tuning.decayPerSecond * 0.5f).Within(1e-4f));
        Assert.That(r.kills, Is.EqualTo(1));
    }

    [Test]
    public void TheGateAsksForComboAndKillsAfterStamina()
    {
        var input = new SkillGateInput
        {
            inBattle = true, alive = true, hasSkill = true, cooldownReady = true, stamina = 10f,
            combo = 19f, comboCost = 20f, kills = 0, killCost = 1,
        };
        Assert.That(SkillGate.Evaluate(input), Is.EqualTo(SkillRejection.NotEnoughCombo));
        input.combo = 20f;
        Assert.That(SkillGate.Evaluate(input), Is.EqualTo(SkillRejection.NotEnoughKills));
        input.kills = 1;
        Assert.That(SkillGate.Evaluate(input), Is.EqualTo(SkillRejection.None));
        input.staminaCost = 50f;
        Assert.That(SkillGate.Evaluate(input), Is.EqualTo(SkillRejection.NotEnoughStamina), "Stamina is checked first.");
    }

    [Test]
    public void AHeroHitAddsComboAndASoldierItKillsAddsAKill()
    {
        using var world = new World("Hero resources");
        var em = world.EntityManager;
        world.SetTime(new Unity.Core.TimeData(5.0, 0.02f));
        var hero = em.CreateEntity(typeof(HeroResourcesComponent));
        var soldier = em.CreateEntity();
        em.AddComponentData(soldier, LocalTransform.FromPosition(float3.zero));
        em.AddComponentData(soldier, new HealthComponent { maxHealth = 100f, currentHealth = 100f });
        em.AddComponentData(soldier, new UnitOwnerComponent());
        var system = world.GetOrCreateSystemManaged<DamageCalculationSystem>();

        Hit(em, hero, soldier, 30f);
        system.Update();
        var r = em.GetComponentData<HeroResourcesComponent>(hero);
        Assert.That(HeroResources.Combo(r, 5.0, Tuning), Is.EqualTo(Tuning.gainPerHit));
        Assert.That(r.kills, Is.EqualTo(0));

        Hit(em, hero, soldier, 500f);
        Hit(em, hero, soldier, 500f); // Same frame, body already at 0: not a second kill.
        system.Update();
        r = em.GetComponentData<HeroResourcesComponent>(hero);
        Assert.That(r.kills, Is.EqualTo(1));
    }

    [Test]
    public void ASkillThatCostsComboWaitsForItAndSpendsIt()
    {
        using var world = new World("Combo skill");
        var em = world.EntityManager;
        em.SetComponentData(em.CreateEntity(typeof(MatchStateComponent)), new MatchStateComponent { currentState = MatchState.InBattle });
        var ability = ScriptableObject.CreateInstance<HeroAbility>();
        ability.abilityName = "Finisher";
        ability.category = HeroAbilityCategory.Ultimate;
        ability.comboCost = 20f;
        ability.phases = new[] { new SkillPhase { duration = 0.5f, interruptible = true, final = true } };
        using var blob = SkillBlobBuilder.Build(ability);
        Object.DestroyImmediate(ability);
        Assert.That(blob.Value.comboCost, Is.EqualTo(20f), "The cost reaches the blob.");

        var hero = em.CreateEntity(typeof(HeroLifeComponent), typeof(HeroCombatComponent), typeof(StaminaComponent),
            typeof(SkillInstanceComponent), typeof(SkillRequestComponent), typeof(HeroResourcesComponent));
        em.SetComponentData(hero, new HeroLifeComponent { isAlive = true });
        em.SetComponentData(hero, new StaminaComponent { currentStamina = 100f, maxStamina = 100f });
        em.AddBuffer<CooldownGroupElement>(hero);
        em.AddBuffer<HeroSkillElement>(hero).Add(new HeroSkillElement { slot = SkillSlot.Ultimate, skillId = blob.Value.id, skill = blob });
        var system = world.GetOrCreateSystemManaged<SkillExecutionSystem>();

        void Press(double t)
        {
            world.SetTime(new Unity.Core.TimeData(t, 0.1f));
            em.SetComponentEnabled<SkillRequestComponent>(hero, true);
            em.SetComponentData(hero, new SkillRequestComponent { slot = SkillSlot.Ultimate, requestedAt = t });
            system.Update();
        }

        Press(1.0);
        Assert.That(em.GetComponentData<SkillInstanceComponent>(hero).active, Is.False, "No combo: it does not start.");

        em.SetComponentData(hero, new HeroResourcesComponent { combo = 25f, lastHitTime = 1.0 });
        Press(2.0);
        Assert.That(em.GetComponentData<SkillInstanceComponent>(hero).active, Is.True);
        Assert.That(HeroResources.Combo(em.GetComponentData<HeroResourcesComponent>(hero), 2.0, Tuning), Is.EqualTo(5f).Within(1e-4f),
            "20 spent.");
    }

    static void Hit(EntityManager em, Entity attacker, Entity target, float damage)
    {
        var hit = em.CreateEntity(typeof(PendingDamageEvent));
        em.SetComponentData(hit, new PendingDamageEvent
        {
            target = target, damageSource = attacker, hasInlineDamageProfile = true, multiplier = 1f,
            inlineDamageProfile = new DamageProfileComponent { bluntDamage = damage }, destroyEntityAfterProcessing = true,
        });
    }
}
