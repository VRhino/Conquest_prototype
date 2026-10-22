using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>Perks as effects with conditions (Aprendizajes_Referencia.md L7-4).</summary>
public class PerkTests
{
    static HeroPerk Perk(int id, bool passive, params PerkEffect[] effects)
    {
        var perk = ScriptableObject.CreateInstance<HeroPerk>();
        perk.perkID = id;
        perk.isPassive = passive;
        perk.effects = effects;
        return perk;
    }

    [Test]
    public void LoadoutTakesUpToFivePassivesAndTwoActivesInOrderAmongTheClassPerks()
    {
        var available = new List<HeroPerk>();
        for (int i = 1; i <= 7; i++) available.Add(Perk(i, true));
        for (int i = 10; i <= 12; i++) available.Add(Perk(i, false));
        var passives = new List<HeroPerk>();
        var actives = new List<HeroPerk>();
        PerkLoadout.Select(available, new[] { 7, 99, 1, 2, 10, 3, 4, 5, 11, 6, 12, 1 }, passives, actives);
        Assert.That(passives.ConvertAll(p => p.perkID), Is.EqualTo(new[] { 7, 1, 2, 3, 4 }), "Unknown, extra and repeated ids are skipped.");
        Assert.That(actives.ConvertAll(p => p.perkID), Is.EqualTo(new[] { 10, 11 }));
        foreach (var p in available) Object.DestroyImmediate(p);
    }

    World _world;
    EntityManager _em;

    [SetUp] public void SetUp() { _world = new World("Perks"); _em = _world.EntityManager; }
    [TearDown] public void TearDown() => _world.Dispose();

    Entity Body(float3 position, Team team)
    {
        var e = _em.CreateEntity(typeof(LocalTransform), typeof(TeamComponent));
        _em.SetComponentData(e, LocalTransform.FromPosition(position));
        _em.SetComponentData(e, new TeamComponent { value = team });
        StatStack.Ensure(_em, e);
        return e;
    }

    void Step(double t, float3 heroPosition, Entity hero, params SystemBase[] systems)
    {
        _world.SetTime(new Unity.Core.TimeData(t, 0.1f));
        _em.SetComponentData(hero, LocalTransform.FromPosition(heroPosition));
        foreach (var s in systems) s.Update();
    }

    [Test]
    public void AnAuraOnTheSquadHoldsWhileTheHeroStaysStillAndFadesWhenItMoves()
    {
        var hero = Body(float3.zero, Team.TeamA);
        var unit = Body(new float3(2f, 0f, 0f), Team.TeamA);
        var squad = _em.CreateEntity();
        _em.AddBuffer<SquadUnitElement>(squad).Add(new SquadUnitElement { Value = unit });
        _em.AddComponentData(hero, new HeroSquadReference { squad = squad });
        _em.AddComponent<HeroMotionComponent>(hero);
        _em.AddBuffer<HeroPerkElement>(hero).Add(new HeroPerkElement
        {
            perkId = 1,
            effect = new PerkEffect { trigger = PerkTrigger.Always, condition = PerkCondition.HeroStill, conditionSeconds = 2f,
                target = PerkTarget.OwnSquad, stat = StatId.SlashDefense, op = ModifierOp.Ratio, value = 0.1f },
        });
        var motion = _world.GetOrCreateSystemManaged<HeroMotionSystem>();
        var aura = _world.GetOrCreateSystemManaged<PerkAuraSystem>();

        for (int i = 0; i <= 15; i++) Step(i * 0.1, float3.zero, hero, motion, aura);
        Assert.That(_em.GetBuffer<StatModifier>(unit).Length, Is.EqualTo(0), "Only 1.5 s still: not yet.");
        for (int i = 16; i <= 25; i++) Step(i * 0.1, float3.zero, hero, motion, aura);
        var mods = _em.GetBuffer<StatModifier>(unit);
        Assert.That(mods.Length, Is.EqualTo(1));
        Assert.That(mods[0].source, Is.EqualTo(ModifierSource.Perk));
        Assert.That(mods[0].stat, Is.EqualTo(StatId.SlashDefense));
        Assert.That(_em.GetBuffer<StatModifier>(hero).Length, Is.EqualTo(0), "The squad, not the hero.");

        double expires = mods[0].expiresAt;
        for (int i = 26; i <= 40; i++) Step(i * 0.1, new float3(i * 0.5f, 0f, 0f), hero, motion, aura); // Moving.
        Assert.That(_em.GetBuffer<StatModifier>(unit)[0].expiresAt, Is.EqualTo(expires).Within(0.3), "No longer refreshed: it runs out.");
        Assert.That(expires, Is.LessThan(3.2));
    }

    [Test]
    public void OnHitEffectsSlowTheTargetWhenTheConditionHolds()
    {
        _world.SetTime(new Unity.Core.TimeData(10.0, 0.1f));
        var hero = Body(float3.zero, Team.TeamA);
        var enemy = Body(new float3(1f, 0f, 0f), Team.TeamB);
        _em.AddComponentData(hero, new HeroMotionComponent { stillFor = 0f, sinceMoved = 0.2f, initialised = true });
        _em.AddBuffer<HeroPerkElement>(hero).Add(new HeroPerkElement
        {
            perkId = 4,
            effect = new PerkEffect { trigger = PerkTrigger.OnHit, condition = PerkCondition.HeroMovedRecently, conditionSeconds = 1f,
                target = PerkTarget.HitTarget, stat = StatId.MoveSpeed, op = ModifierOp.Ratio, value = -0.3f, duration = 1.5f },
        });
        _em.SetComponentData(_em.CreateEntity(typeof(PendingDamageEvent)), new PendingDamageEvent { target = enemy, damageSource = hero });
        _world.GetOrCreateSystemManaged<PerkOnHitSystem>().Update();

        var mods = _em.GetBuffer<StatModifier>(enemy);
        Assert.That(mods.Length, Is.EqualTo(1));
        Assert.That(mods[0].value, Is.EqualTo(-0.3f));
        Assert.That(mods[0].expiresAt, Is.EqualTo(11.5).Within(1e-6));
        Assert.That(_em.IsComponentEnabled<ModifiersDirty>(enemy), Is.True);

        _em.SetComponentData(hero, new HeroMotionComponent { stillFor = 3f, sinceMoved = 3f, initialised = true });
        _em.GetBuffer<StatModifier>(enemy).Clear();
        _world.GetOrCreateSystemManaged<PerkOnHitSystem>().Update();
        Assert.That(_em.GetBuffer<StatModifier>(enemy).Length, Is.EqualTo(0), "Condition not met: nothing.");
    }
}
