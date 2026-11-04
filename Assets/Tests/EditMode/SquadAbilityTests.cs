using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>Squad abilities launched through the hero's gate and timeline (Aprendizajes_Referencia.md L2-9).</summary>
public class SquadAbilityTests
{
    World _world;
    EntityManager _em;
    BlobAssetReference<SkillBlob> _brace;
    BlobAssetReference<SkillBlob> _volley;

    [SetUp]
    public void SetUp()
    {
        _world = new World("Squad abilities");
        _em = _world.EntityManager;
        _em.SetComponentData(_em.CreateEntity(typeof(MatchStateComponent)), new MatchStateComponent { currentState = MatchState.InBattle });
        _brace = Build("Test Brace", 30f, new SkillEvent
        {
            type = SkillEventType.ApplyModifier, target = SkillTarget.OwnSquad, stat = StatId.SlashDefense, op = ModifierOp.Ratio,
            value = 0.25f, duration = 8f,
        }, SkillBlobBuilder.SquadSlot(0));
        _volley = Build("Test Volley", 20f, new SkillEvent
        {
            type = SkillEventType.ApplyModifier, target = SkillTarget.Self, stat = StatId.PierceDamage, op = ModifierOp.Ratio,
            value = 0.2f, duration = 6f,
        }, SkillBlobBuilder.SquadSlot(1));
    }

    [TearDown]
    public void TearDown()
    {
        _world.Dispose();
        _brace.Dispose();
        _volley.Dispose();
    }

    static BlobAssetReference<SkillBlob> Build(string name, float cooldown, SkillEvent effect, SkillSlot slot)
    {
        var ability = ScriptableObject.CreateInstance<AbilityData>();
        ability.abilityName = name;
        ability.cooldown = cooldown;
        ability.events = new[] { effect };
        var blob = SkillBlobBuilder.BuildSquadAbility(ability, slot);
        Object.DestroyImmediate(ability);
        return blob;
    }

    Entity Definition(BlobAssetReference<SkillBlob> skill, SkillSlot slot)
    {
        var entity = _em.CreateEntity();
        _em.AddComponentData(entity, new SquadAbilityDefinitionComponent { slot = slot, skill = skill, cooldown = skill.Value.cooldown });
        return entity;
    }

    /// <summary>A spawned squad: caster components as <see cref="SquadSpawningSystem"/> adds them, and living units.</summary>
    Entity Squad(int level, int units)
    {
        var squad = _em.CreateEntity(typeof(SquadProgressComponent), typeof(SquadDataComponent), typeof(SquadDataReference),
            typeof(TeamComponent), typeof(SkillRequestComponent), typeof(SkillInstanceComponent));
        _em.SetComponentData(squad, new SquadProgressComponent { level = level, xpToNextLevel = 100 });
        _em.SetComponentData(squad, new SquadDataReference { dataEntity = squad });
        _em.SetComponentData(squad, new SquadDataComponent { baseHealth = 100, baseSpeed = 5 });
        _em.SetComponentData(squad, new TeamComponent { value = Team.TeamA });
        _em.SetComponentEnabled<SkillRequestComponent>(squad, false);
        _em.AddBuffer<HeroSkillElement>(squad);
        _em.AddBuffer<CooldownGroupElement>(squad);
        var brace = Definition(_brace, SkillBlobBuilder.SquadSlot(0));
        var volley = Definition(_volley, SkillBlobBuilder.SquadSlot(1));
        var abilities = _em.AddBuffer<AbilityByLevelElement>(squad);
        abilities.Add(new AbilityByLevelElement { Value = brace });
        abilities.Add(new AbilityByLevelElement { Value = volley });
        _em.AddBuffer<SquadUnitElement>(squad);
        for (int i = 0; i < units; i++)
        {
            var unit = _em.CreateEntity(typeof(TeamComponent));
            _em.AddComponentData(unit, LocalTransform.FromPosition(new float3(i, 0f, 0f)));
            _em.SetComponentData(unit, new TeamComponent { value = Team.TeamA });
            _em.AddBuffer<StatModifier>(unit);
            _em.GetBuffer<SquadUnitElement>(squad).Add(new SquadUnitElement { Value = unit });
        }
        return squad;
    }

    void Step(Entity squad, double t, SkillSlot? slot)
    {
        _world.SetTime(new Unity.Core.TimeData(t, 0.1f));
        _em.SetComponentEnabled<SkillRequestComponent>(squad, slot.HasValue);
        if (slot.HasValue) _em.SetComponentData(squad, new SkillRequestComponent { slot = slot.Value, requestedAt = t });
        _world.GetOrCreateSystemManaged<SkillExecutionSystem>().Update();
        _world.GetOrCreateSystemManaged<SkillEffectSystem>().Update();
    }

    int Modifiers(Entity squad, StatId stat)
    {
        int count = 0;
        foreach (var unit in _em.GetBuffer<SquadUnitElement>(squad))
            foreach (var m in _em.GetBuffer<StatModifier>(unit.Value))
                if (m.stat == stat) count++;
        return count;
    }

    [Test]
    public void EachAbilityBecomesLaunchableAtItsUnlockLevelOnce()
    {
        var squad = Squad(SquadProgressionCurves.DefaultUnlockLevel(0), 1);
        var scaling = _world.GetOrCreateSystemManaged<UnitStatScalingSystem>();
        scaling.Update();
        var skills = _em.GetBuffer<HeroSkillElement>(squad);
        Assert.That(skills.Length, Is.EqualTo(1), "Only the first ability is unlocked at its level.");
        Assert.That(skills[0].slot, Is.EqualTo(SkillSlot.Squad1));

        _em.SetComponentData(squad, new SquadProgressComponent { level = SquadProgressionCurves.DefaultUnlockLevel(1), xpToNextLevel = 100 });
        _em.SetComponentData(_em.CreateEntity(typeof(SquadLevelUpEvent)), new SquadLevelUpEvent { squad = squad });
        scaling.Update();
        _em.SetComponentData(_em.CreateEntity(typeof(SquadLevelUpEvent)), new SquadLevelUpEvent { squad = squad });
        scaling.Update();
        skills = _em.GetBuffer<HeroSkillElement>(squad);
        Assert.That(skills.Length, Is.EqualTo(2), "The second unlocks at its level, and nothing is added twice.");
        Assert.That(skills[1].slot, Is.EqualTo(SkillSlot.Squad2));
    }

    [Test]
    public void ALockedAbilityIsNotLaunched()
    {
        var squad = Squad(1, 2);
        _world.GetOrCreateSystemManaged<UnitStatScalingSystem>().Update();
        Step(squad, 0.0, SkillSlot.Squad1);
        Assert.That(_em.GetComponentData<SkillInstanceComponent>(squad).active, Is.False);
        Assert.That(Modifiers(squad, StatId.SlashDefense), Is.EqualTo(0));
    }

    [Test]
    public void TheSquadLaunchesOnItsUnitsAndCoolsDownOnItsOwn()
    {
        var squad = Squad(SquadProgressionCurves.DefaultUnlockLevel(1), 3);
        _world.GetOrCreateSystemManaged<UnitStatScalingSystem>().Update();

        Step(squad, 0.0, SkillSlot.Squad1);
        Assert.That(Modifiers(squad, StatId.SlashDefense), Is.EqualTo(3), "Own squad = every unit of the squad.");
        var groups = _em.GetBuffer<CooldownGroupElement>(squad);
        Assert.That(CooldownUtility.IsReady(groups, _brace.Value.cooldownGroup, 1.0), Is.False, "The squad's own cooldown group.");

        Step(squad, 0.1, null);
        Step(squad, 0.2, null);
        Assert.That(_em.GetComponentData<SkillInstanceComponent>(squad).active, Is.False, "One short cast phase.");
        Step(squad, 1.0, SkillSlot.Squad2);
        Assert.That(Modifiers(squad, StatId.PierceDamage), Is.EqualTo(3), "Another slot is not blocked by the first one's cooldown; self = its units.");

        Step(squad, 2.0, SkillSlot.Squad1);
        Assert.That(_em.GetComponentData<SkillInstanceComponent>(squad).active, Is.False, "Still cooling down.");
        Step(squad, 31.0, SkillSlot.Squad1);
        Assert.That(_em.GetComponentData<SkillInstanceComponent>(squad).active, Is.True, "Ready again after its cooldown.");
    }

    [Test]
    public void TheSquadKeysOfTheLocalHeroRequestOnItsActiveSquad()
    {
        var squad = Squad(1, 1);
        var hero = _em.CreateEntity(typeof(IsLocalPlayer), typeof(HeroInputComponent), typeof(SkillRequestComponent));
        _em.SetComponentEnabled<SkillRequestComponent>(hero, false);
        _em.AddComponentData(hero, new HeroSquadReference { squad = squad });
        _em.SetComponentData(hero, new HeroInputComponent { UseSquadAbility2 = true });
        _world.GetOrCreateSystemManaged<HeroSkillRequestSystem>().Update();
        Assert.That(_em.IsComponentEnabled<SkillRequestComponent>(squad), Is.True);
        Assert.That(_em.GetComponentData<SkillRequestComponent>(squad).slot, Is.EqualTo(SkillSlot.Squad2));
        Assert.That(_em.GetComponentData<SkillRequestComponent>(squad).heldSlots, Is.EqualTo(SkillRequestComponent.Bit(SkillSlot.Squad2)));
        Assert.That(_em.IsComponentEnabled<SkillRequestComponent>(hero), Is.False, "The hero itself launches nothing.");
    }

    [Test]
    public void ASquadWithoutLivingUnitsLaunchesNothing()
    {
        var squad = Squad(SquadProgressionCurves.DefaultUnlockLevel(0), 2);
        _world.GetOrCreateSystemManaged<UnitStatScalingSystem>().Update();
        foreach (var unit in _em.GetBuffer<SquadUnitElement>(squad).ToNativeArray(Unity.Collections.Allocator.Temp))
            _em.AddComponent<IsDeadComponent>(unit.Value);
        Step(squad, 0.0, SkillSlot.Squad1);
        Assert.That(_em.GetComponentData<SkillInstanceComponent>(squad).active, Is.False);
    }

    [Test]
    public void ASquadAbilityRunsAtTheMeanAttackSpeedOfItsLivingUnits()
    {
        var ability = ScriptableObject.CreateInstance<AbilityData>();
        ability.abilityName = "Test Rush";
        ability.cooldown = 10f;
        ability.phases = new[]
        {
            new SkillPhase { duration = 1f, scalesWithAttackSpeed = true },
            new SkillPhase { duration = 1f, final = true },
        };
        using var rush = SkillBlobBuilder.BuildSquadAbility(ability, SkillSlot.Squad1);
        Object.DestroyImmediate(ability);
        var squad = Squad(SquadProgressionCurves.DefaultUnlockLevel(0), 3);
        _world.GetOrCreateSystemManaged<UnitStatScalingSystem>().Update();
        var skills = _em.GetBuffer<HeroSkillElement>(squad);
        skills[0] = new HeroSkillElement { slot = SkillSlot.Squad1, skillId = rush.Value.id, skill = rush };
        var units = _em.GetBuffer<SquadUnitElement>(squad).ToNativeArray(Unity.Collections.Allocator.Temp);
        float[] speeds = { 3f, 1f, 10f };
        for (int i = 0; i < units.Length; i++)
            _em.AddComponentData(units[i].Value, new CombatStatsComponent { attackSpeed = speeds[i] });
        _em.AddComponent<IsDeadComponent>(units[2].Value); // The fallen one does not count.
        units.Dispose();

        Step(squad, 0.0, SkillSlot.Squad1);
        float before = _em.GetComponentData<SkillInstanceComponent>(squad).phaseTime;
        Step(squad, 0.1, null);
        Assert.That(_em.GetComponentData<SkillInstanceComponent>(squad).phaseTime - before, Is.EqualTo(0.2f).Within(1e-5f),
            "Mean of 3 and 1: twice as fast.");
    }
}
