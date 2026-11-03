using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>Attacker and receiver conditional modifiers, evaluated only at impact (Aprendizajes_Referencia.md L1-02).</summary>
public class ConditionalModifierTests
{
    static readonly HitParty Hero = new HitParty { isHero = true, totalArmor = 30f, healthFraction = 1f };
    static readonly HitParty Pikeman = new HitParty
    {
        isSoldier = true, squadType = SquadType.Pikemen, unitType = UnitType.Infantry, totalArmor = 60f, healthFraction = 0.5f,
    };
    static readonly HitFacts Melee = new HitFacts { form = HitForm.Melee };

    static bool Matches(HitCondition c, HitParty self, HitParty other, HitFacts facts = default,
        bool selfEffect = false, bool otherEffect = false, float roll = 0f)
        => HitConditions.Matches(c, self, other, facts, selfEffect, otherEffect, roll);

    [Test]
    public void TheDefaultConditionAlwaysHolds()
        => Assert.That(Matches(default, Hero, Pikeman, Melee), Is.True);

    [Test]
    public void KindClassAndUnitTypeOfTheOtherPartyAreAnyOfLists()
    {
        Assert.That(Matches(new HitCondition { otherKind = PerkTargetKind.Hero }, Pikeman, Hero), Is.True);
        Assert.That(Matches(new HitCondition { otherKind = PerkTargetKind.Hero }, Hero, Pikeman), Is.False);
        Assert.That(Matches(new HitCondition { otherKind = PerkTargetKind.Soldier }, Hero, Pikeman), Is.True);
        var classes = new HitCondition { otherSquadTypes = PerkSquadTypes.Pikemen | PerkSquadTypes.Spearmen };
        Assert.That(Matches(classes, Hero, Pikeman), Is.True);
        Assert.That(Matches(classes, Pikeman, Hero), Is.False, "A hero is no soldier class.");
        Assert.That(Matches(new HitCondition { otherSquadTypes = PerkSquadTypes.Archers }, Hero, Pikeman), Is.False);
        Assert.That(Matches(new HitCondition { otherUnitTypes = UnitTypes.Cavalry }, Hero, Pikeman), Is.False,
            "\"+X % against cavalry\" does not touch infantry.");
        Assert.That(Matches(new HitCondition { otherUnitTypes = UnitTypes.Infantry | UnitTypes.Cavalry }, Hero, Pikeman), Is.True);
    }

    [Test]
    public void FormSkillAndCriticalOfTheHit()
    {
        Assert.That(Matches(new HitCondition { forms = HitForms.Ranged | HitForms.Ability }, Hero, Pikeman, Melee), Is.False);
        Assert.That(Matches(new HitCondition { forms = HitForms.Melee }, Hero, Pikeman, Melee), Is.True);
        var skills = new HitCondition { skillIds = new int4(5, 9, 0, 0) };
        Assert.That(Matches(skills, Hero, Pikeman, new HitFacts { skillId = 9 }), Is.True);
        Assert.That(Matches(skills, Hero, Pikeman, new HitFacts { skillId = 4 }), Is.False);
        Assert.That(Matches(skills, Hero, Pikeman, new HitFacts()), Is.False, "No skill: not one of the list.");
        Assert.That(Matches(new HitCondition { critical = PerkTristate.Must }, Hero, Pikeman, new HitFacts { critical = true }), Is.True);
        Assert.That(Matches(new HitCondition { critical = PerkTristate.MustNot }, Hero, Pikeman, new HitFacts { critical = true }), Is.False);
    }

    [Test]
    public void EffectsHealthArmorAndChance()
    {
        Assert.That(Matches(new HitCondition { otherHasEffect = PerkTristate.Must }, Hero, Pikeman, otherEffect: true), Is.True);
        Assert.That(Matches(new HitCondition { selfHasEffect = PerkTristate.MustNot }, Hero, Pikeman, selfEffect: true), Is.False);
        // As attacker the receiver is the other (the pikeman, at half health); as defender it is the owner.
        var wounded = new HitCondition { defenderHealthMax = 0.6f };
        Assert.That(Matches(wounded, Hero, Pikeman), Is.True);
        wounded.side = HitSide.Defender;
        Assert.That(Matches(wounded, Hero, Pikeman), Is.False, "The defending hero is at full health.");
        Assert.That(Matches(new HitCondition { defenderHealthMin = 0.75f }, Hero, Pikeman), Is.False);
        Assert.That(Matches(new HitCondition { otherArmorMin = 50f }, Hero, Pikeman), Is.True);
        Assert.That(Matches(new HitCondition { otherArmorMax = 50f }, Hero, Pikeman), Is.False);
        Assert.That(Matches(new HitCondition { chance = 0.3f }, Hero, Pikeman, roll: 0.29f), Is.True);
        Assert.That(Matches(new HitCondition { chance = 0.3f }, Hero, Pikeman, roll: 0.31f), Is.False);
    }

    [Test]
    public void DamageAddsTheRatiosOfBothSidesOnce()
    {
        using var world = new World("Conditional modifiers");
        var em = world.EntityManager;
        var attacker = em.CreateEntity(typeof(HealthComponent));
        em.SetComponentData(attacker, new HealthComponent { maxHealth = 100f, currentHealth = 100f });
        var modifiers = em.AddBuffer<ConditionalModifier>(attacker);
        modifiers.Add(new ConditionalModifier { condition = new HitCondition { otherKind = PerkTargetKind.Hero }, damageRatio = 0.5f });
        modifiers.Add(new ConditionalModifier { condition = new HitCondition { forms = HitForms.Ranged }, damageRatio = 1f });
        modifiers.Add(new ConditionalModifier { condition = new HitCondition { side = HitSide.Defender }, damageRatio = 3f });

        var hero = em.CreateEntity(typeof(HeroHealthComponent), typeof(HeroLifeComponent));
        em.SetComponentData(hero, new HeroHealthComponent { maxHealth = 1000f, currentHealth = 1000f });
        em.AddBuffer<ConditionalModifier>(hero).Add(new ConditionalModifier
        {
            condition = new HitCondition { side = HitSide.Defender, forms = HitForms.Melee }, damageRatio = -0.25f,
        });
        var damage = world.GetOrCreateSystemManaged<DamageCalculationSystem>();

        Hit(em, attacker, hero, 100f);
        damage.Update();
        // +0.5 (against a hero) − 0.25 (the hero takes less from melee) = ×1.25; the ranged and defender-side
        // modifiers of the attacker do not count.
        Assert.That(em.GetComponentData<HeroHealthComponent>(hero).currentHealth, Is.EqualTo(875f).Within(1e-3f));

        var soldier = em.CreateEntity(typeof(HealthComponent));
        em.SetComponentData(soldier, new HealthComponent { maxHealth = 1000f, currentHealth = 1000f });
        Hit(em, attacker, soldier, 100f);
        damage.Update();
        Assert.That(em.GetComponentData<HealthComponent>(soldier).currentHealth, Is.EqualTo(900f).Within(1e-3f),
            "Against a soldier none holds.");
    }

    [Test]
    public void ARecastReplacesItsOwnAndDropsExpiredOnes()
    {
        using var world = new World("Conditional replace");
        var em = world.EntityManager;
        var e = em.CreateEntity();
        var buffer = em.AddBuffer<ConditionalModifier>(e);
        buffer.Add(new ConditionalModifier { source = ModifierSource.Effect, sourceId = 7, damageRatio = 0.1f, expiresAt = 10 });
        buffer.Add(new ConditionalModifier { source = ModifierSource.Effect, sourceId = 8, damageRatio = 0.1f, expiresAt = 2 });
        buffer.Add(new ConditionalModifier { source = ModifierSource.UnitType, damageRatio = 0.2f });
        HitConditions.Replace(buffer, ModifierSource.Effect, 7,
            new ConditionalModifier { source = ModifierSource.Effect, sourceId = 7, damageRatio = 0.3f, expiresAt = 20 }, 5);
        Assert.That(buffer.Length, Is.EqualTo(2));
        Assert.That(buffer[0].source, Is.EqualTo(ModifierSource.UnitType));
        Assert.That(buffer[1].damageRatio, Is.EqualTo(0.3f));
    }

    static void Hit(EntityManager em, Entity attacker, Entity target, float blunt)
    {
        var hit = em.CreateEntity(typeof(PendingDamageEvent));
        em.SetComponentData(hit, new PendingDamageEvent
        {
            target = target, damageSource = attacker, hasInlineDamageProfile = true,
            inlineDamageProfile = new DamageProfileComponent { bluntDamage = blunt },
            multiplier = 1f, destroyEntityAfterProcessing = true, hitForm = HitForm.Melee,
        });
    }
}
