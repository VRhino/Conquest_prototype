using NUnit.Framework;
using Unity.Entities;
using UnityEngine;

/// <summary>Interrupt vs yield and the external endings of a skill (Aprendizajes_Referencia.md L2-4).</summary>
public class SkillInterruptTests
{
    World _world;
    EntityManager _em;
    Entity _hero;
    SkillExecutionSystem _system;
    BlobAssetReference<SkillBlob> _long, _quick;

    static BlobAssetReference<SkillBlob> Skill(string name, HeroAbilityCategory category, SkillRefresh refresh, float windup, float recovery)
    {
        var ability = ScriptableObject.CreateInstance<HeroAbility>();
        ability.abilityName = name;
        ability.category = category;
        ability.refresh = refresh;
        ability.phases = new[]
        {
            new SkillPhase { duration = windup, interruptible = true },
            new SkillPhase { duration = recovery, interruptible = true, final = true },
        };
        var blob = SkillBlobBuilder.Build(ability);
        Object.DestroyImmediate(ability);
        return blob;
    }

    [SetUp]
    public void SetUp()
    {
        _world = new World("Skill interrupts");
        _em = _world.EntityManager;
        _em.SetComponentData(_em.CreateEntity(typeof(MatchStateComponent)), new MatchStateComponent { currentState = MatchState.InBattle });
        _long = Skill("Long", HeroAbilityCategory.Q, SkillRefresh.Interrupt, 2f, 1f);
        _quick = Skill("Quick", HeroAbilityCategory.E, SkillRefresh.Yield, 0.3f, 0.2f);
        _hero = _em.CreateEntity(typeof(HeroLifeComponent), typeof(HeroCombatComponent), typeof(StaminaComponent),
            typeof(SkillInstanceComponent), typeof(SkillRequestComponent), typeof(ParkedSkillComponent), typeof(EndSkillsRequestComponent));
        _em.SetComponentData(_hero, new HeroLifeComponent { isAlive = true });
        _em.SetComponentData(_hero, new StaminaComponent { currentStamina = 100f, maxStamina = 100f });
        _em.SetComponentEnabled<EndSkillsRequestComponent>(_hero, false);
        _em.AddBuffer<CooldownGroupElement>(_hero);
        var skills = _em.AddBuffer<HeroSkillElement>(_hero);
        skills.Add(new HeroSkillElement { slot = SkillSlot.Q, skillId = _long.Value.id, skill = _long });
        skills.Add(new HeroSkillElement { slot = SkillSlot.E, skillId = _quick.Value.id, skill = _quick });
        _system = _world.GetOrCreateSystemManaged<SkillExecutionSystem>();
    }

    [TearDown]
    public void TearDown()
    {
        _world.Dispose();
        _long.Dispose();
        _quick.Dispose();
    }

    void Step(double t, SkillSlot? slot = null)
    {
        _world.SetTime(new Unity.Core.TimeData(t, 0.1f));
        _em.SetComponentEnabled<SkillRequestComponent>(_hero, slot.HasValue);
        if (slot.HasValue) _em.SetComponentData(_hero, new SkillRequestComponent { slot = slot.Value, requestedAt = t });
        _system.Update();
    }

    SkillInstanceComponent Current => _em.GetComponentData<SkillInstanceComponent>(_hero);
    ParkedSkillComponent Parked => _em.GetComponentData<ParkedSkillComponent>(_hero);

    [Test]
    public void AYieldSkillParksTheCurrentOneAndRestoresItWithItsClockRunning()
    {
        Step(0.0, SkillSlot.Q);
        Step(0.1);
        Step(0.2, SkillSlot.E); // Quick is "yield": Long goes aside.
        Assert.That(Current.skillIndex, Is.EqualTo(1));
        Assert.That(Parked.active, Is.True);
        Assert.That(Parked.instance.skillIndex, Is.EqualTo(0));

        for (int i = 3; i <= 8; i++) Step(i * 0.1); // Quick lasts 0.5 s.
        Assert.That(Current.skillIndex, Is.EqualTo(0), "Long is current again.");
        Assert.That(Current.active, Is.True);
        Assert.That(Current.phaseTime, Is.GreaterThan(0.6f), "Its clock kept running while parked.");
        Assert.That(Parked.active, Is.False);
    }

    [Test]
    public void AnInterruptSkillReplacesWithoutParking()
    {
        Step(0.0, SkillSlot.E);
        Step(0.1, SkillSlot.Q);
        Assert.That(Current.skillIndex, Is.EqualTo(0));
        Assert.That(Parked.active, Is.False);
    }

    [Test]
    public void ASquadSwapRequestOrAStunEndsTheCurrentAndTheParkedSkill()
    {
        Step(0.0, SkillSlot.Q);
        Step(0.1, SkillSlot.E);
        _em.SetComponentEnabled<EndSkillsRequestComponent>(_hero, true);
        Step(0.2);
        Assert.That(Current.active, Is.False);
        Assert.That(Parked.active, Is.False);
        Assert.That(_em.IsComponentEnabled<EndSkillsRequestComponent>(_hero), Is.False, "The request is consumed.");

        Step(10.0, SkillSlot.E);
        Assert.That(Current.active, Is.True);
        _em.AddComponentData(_hero, new MoveSpeedComponent { value = 0f });
        Step(10.1);
        Assert.That(Current.active, Is.False, "Control ends the skill.");
    }
}
