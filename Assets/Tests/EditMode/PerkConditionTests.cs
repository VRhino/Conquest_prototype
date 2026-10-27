using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>Richer perk conditions (Aprendizajes_Referencia.md L2-15): AND of conditions, "any of" lists, tri-states.</summary>
public class PerkConditionTests
{
    static readonly PerkSourceContext AnySource = new() { roll = 0.5f };
    static readonly PerkTargetContext AnyTarget = default;

    [Test]
    public void DefaultsAskNothingSoOlderDataKeepsItsMeaning()
    {
        var effect = new PerkEffect();
        Assert.That(PerkConditions.Met(effect, AnySource, AnyTarget), Is.True);
        Assert.That(PerkConditions.NeedsTarget(effect), Is.False);
    }

    [Test]
    public void ChanceAppliesBelowTheRollOnlyAndZeroOrOneMeanAlways()
    {
        var effect = new PerkEffect { chance = 0.3f };
        Assert.That(PerkConditions.MetBySource(effect, new PerkSourceContext { roll = 0.29f }), Is.True);
        Assert.That(PerkConditions.MetBySource(effect, new PerkSourceContext { roll = 0.31f }), Is.False);
        effect.chance = 1f;
        Assert.That(PerkConditions.MetBySource(effect, new PerkSourceContext { roll = 0.99f }), Is.True);
    }

    [Test]
    public void TheSeededRollIsStablePerTickAndTargetAndMatchesTheChanceOverManyHits()
    {
        var source = new Entity { Index = 3, Version = 1 };
        var a = new Entity { Index = 10, Version = 1 };
        var b = new Entity { Index = 11, Version = 1 };
        Assert.That(PerkConditions.Roll(500, source, a, 7), Is.EqualTo(PerkConditions.Roll(500, source, a, 7)), "Replayable.");
        Assert.That(PerkConditions.Roll(500, source, a, 7), Is.Not.EqualTo(PerkConditions.Roll(500, source, b, 7)),
            "Two targets of one swing roll apart.");
        var effect = new PerkEffect { chance = 0.25f };
        int met = 0;
        for (uint tick = 0; tick < 4000; tick++)
            if (PerkConditions.MetBySource(effect, new PerkSourceContext { roll = PerkConditions.Roll(tick, source, a, 7) })) met++;
        Assert.That(met / 4000f, Is.EqualTo(0.25f).Within(0.03f));
    }

    [Test]
    public void TargetKindAndSoldierClassesAreAnyOf()
    {
        var hero = new PerkTargetContext { isHero = true };
        var archer = new PerkTargetContext { isSoldier = true, squadType = SquadType.Archers };
        var squire = new PerkTargetContext { isSoldier = true, squadType = SquadType.Squires };

        var heroesOnly = new PerkEffect { targetKind = PerkTargetKind.Hero };
        Assert.That(PerkConditions.MetByTarget(heroesOnly, hero), Is.True);
        Assert.That(PerkConditions.MetByTarget(heroesOnly, archer), Is.False);
        var soldiers = new PerkEffect { targetKind = PerkTargetKind.Soldier };
        Assert.That(PerkConditions.MetByTarget(soldiers, hero), Is.False);
        Assert.That(PerkConditions.MetByTarget(soldiers, squire), Is.True);

        var ranged = new PerkEffect { targetSquadTypes = PerkSquadTypes.Archers | PerkSquadTypes.Pikemen };
        Assert.That(PerkConditions.NeedsTarget(ranged), Is.True);
        Assert.That(PerkConditions.MetByTarget(ranged, archer), Is.True);
        Assert.That(PerkConditions.MetByTarget(ranged, squire), Is.False);
        Assert.That(PerkConditions.MetByTarget(ranged, hero), Is.False, "A class list only matches soldiers.");
    }

    [TestCase(PerkTristate.Any, false, true)]
    [TestCase(PerkTristate.Any, true, true)]
    [TestCase(PerkTristate.Must, true, true)]
    [TestCase(PerkTristate.Must, false, false)]
    [TestCase(PerkTristate.MustNot, true, false)]
    [TestCase(PerkTristate.MustNot, false, true)]
    public void EffectTristatesOnTargetAndSelf(PerkTristate condition, bool hasEffect, bool met)
    {
        Assert.That(PerkConditions.MetByTarget(new PerkEffect { targetHasEffect = condition },
            new PerkTargetContext { hasEffect = hasEffect }), Is.EqualTo(met));
        Assert.That(PerkConditions.MetBySource(new PerkEffect { selfHasEffect = condition },
            new PerkSourceContext { selfHasEffect = hasEffect }), Is.EqualTo(met));
    }

    [Test]
    public void SquadAliveRangeNeedsASquadAndAZeroMaxHasNoUpperLimit()
    {
        var lastStand = new PerkEffect { squadAliveMax = 0.5f };
        Assert.That(PerkConditions.MetBySource(lastStand, new PerkSourceContext { hasSquad = true, squadAliveFraction = 0.4f }), Is.True);
        Assert.That(PerkConditions.MetBySource(lastStand, new PerkSourceContext { hasSquad = true, squadAliveFraction = 0.6f }), Is.False);
        Assert.That(PerkConditions.MetBySource(lastStand, new PerkSourceContext { hasSquad = false }), Is.False, "No squad: not in range.");
        var strong = new PerkEffect { squadAliveMin = 0.8f };
        Assert.That(PerkConditions.MetBySource(strong, new PerkSourceContext { hasSquad = true, squadAliveFraction = 1f }), Is.True);
        Assert.That(PerkConditions.MetBySource(strong, new PerkSourceContext { hasSquad = true, squadAliveFraction = 0.7f }), Is.False);
    }

    [Test]
    public void ConditionsCombineWithAnd()
    {
        var effect = new PerkEffect
        {
            condition = PerkCondition.HeroStill, conditionSeconds = 1f, chance = 0.5f, targetKind = PerkTargetKind.Soldier,
            targetHasEffect = PerkTristate.Must, squadAliveMin = 0.5f,
        };
        var source = new PerkSourceContext { motion = new HeroMotionComponent { stillFor = 2f }, roll = 0.1f, hasSquad = true, squadAliveFraction = 0.9f };
        var target = new PerkTargetContext { isSoldier = true, hasEffect = true };
        Assert.That(PerkConditions.Met(effect, source, target), Is.True);

        var moving = source; moving.motion.stillFor = 0f;
        Assert.That(PerkConditions.Met(effect, moving, target), Is.False);
        var unlucky = source; unlucky.roll = 0.9f;
        Assert.That(PerkConditions.Met(effect, unlucky, target), Is.False);
        var clean = target; clean.hasEffect = false;
        Assert.That(PerkConditions.Met(effect, source, clean), Is.False);
        var hero = new PerkTargetContext { isHero = true, hasEffect = true };
        Assert.That(PerkConditions.Met(effect, source, hero), Is.False);
    }

    [Test]
    public void OnlyAnActiveTimedEffectCounts()
    {
        using var world = new World("Perk effects");
        var em = world.EntityManager;
        var e = em.CreateEntity();
        var mods = em.AddBuffer<StatModifier>(e);
        mods.Add(new StatModifier { stat = StatId.MoveSpeed, source = ModifierSource.Perk, expiresAt = 20.0 });
        mods.Add(new StatModifier { stat = StatId.SlashDefense, source = ModifierSource.Effect, expiresAt = 5.0 });
        Assert.That(PerkConditions.HasActiveEffect(mods, StatId.MoveSpeed, 10.0), Is.False, "A perk is not an effect.");
        Assert.That(PerkConditions.HasActiveEffect(mods, StatId.SlashDefense, 4.0), Is.True);
        Assert.That(PerkConditions.HasActiveEffect(mods, StatId.SlashDefense, 6.0), Is.False, "Expired.");
    }

    [Test]
    public void TheSystemsGatherTheTargetClassAndTheSquadThatIsLeft()
    {
        using var world = new World("Perk context");
        var em = world.EntityManager;
        world.SetTime(new Unity.Core.TimeData(10.0, 0.1f));
        var hero = em.CreateEntity(typeof(LocalTransform), typeof(TeamComponent), typeof(HeroLifeComponent));
        StatStack.Ensure(em, hero);
        var archers = em.CreateEntity(typeof(SquadDefinitionComponent));
        em.SetComponentData(archers, new SquadDefinitionComponent { squadType = SquadType.Archers });
        var archer = em.CreateEntity(typeof(LocalTransform), typeof(TeamComponent));
        em.AddComponentData(archer, new UnitOwnerComponent { squad = archers });
        StatStack.Ensure(em, archer);
        var squires = em.CreateEntity(typeof(SquadDefinitionComponent));
        var squire = em.CreateEntity(typeof(LocalTransform), typeof(TeamComponent));
        em.AddComponentData(squire, new UnitOwnerComponent { squad = squires });
        StatStack.Ensure(em, squire);

        // The hero's own squad has 1 of 4 soldiers left.
        var own = em.CreateEntity();
        em.AddComponentData(own, new SquadInstanceComponent { initialUnitCount = 4 });
        var ownUnit = em.CreateEntity();
        em.AddBuffer<SquadUnitElement>(own).Add(new SquadUnitElement { Value = ownUnit });
        em.AddComponentData(hero, new HeroSquadReference { squad = own });

        var effect = new PerkEffect
        {
            trigger = PerkTrigger.OnHit, target = PerkTarget.HitTarget, targetSquadTypes = PerkSquadTypes.Archers,
            squadAliveMax = 0.5f, stat = StatId.MoveSpeed, op = ModifierOp.Ratio, value = -0.2f, duration = 2f,
        };
        em.AddBuffer<HeroPerkElement>(hero).Add(new HeroPerkElement { perkId = 9, effect = effect });
        foreach (var target in new[] { archer, squire })
            em.SetComponentData(em.CreateEntity(typeof(PendingDamageEvent)), new PendingDamageEvent { target = target, damageSource = hero });
        world.GetOrCreateSystemManaged<PerkOnHitSystem>().Update();

        Assert.That(em.GetBuffer<StatModifier>(archer).Length, Is.EqualTo(1), "An archer, with a quarter of the squad left.");
        Assert.That(em.GetBuffer<StatModifier>(squire).Length, Is.EqualTo(0), "Not in the class list.");
        var context = PerkContext.Source(em, hero, effect, 10.0, 0f);
        Assert.That(context.hasSquad, Is.True);
        Assert.That(context.squadAliveFraction, Is.EqualTo(0.25f));
    }
}
