using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>The AI uses its skills (H1) and chooses an ally to support (L4-08).</summary>
public class HeroAISkillTests
{
    static readonly HeroAISkillTuning Tuning = HeroAISkillTuning.Default;

    static BlobAssetReference<SkillBlob> Ability(string name, HeroAbilityCategory category, params SkillEvent[] events)
    {
        var ability = ScriptableObject.CreateInstance<HeroAbility>();
        ability.abilityName = name;
        ability.category = category;
        ability.cooldown = 10f;
        ability.phases = new[] { new SkillPhase { duration = 0.2f, interruptible = true, final = true } };
        ability.events = events;
        var blob = SkillBlobBuilder.Build(ability);
        Object.DestroyImmediate(ability);
        return blob;
    }

    static SkillEvent Area(float radius, float distance) => new() { type = SkillEventType.AreaDamage, radius = radius, distance = distance, value = 1f };
    static SkillEvent Buff(SkillTarget target, float radius) => new() { type = SkillEventType.ApplyModifier, target = target, radius = radius, stat = StatId.SlashDefense, value = 0.3f, duration = 5f };

    [Test]
    public void ASkillsPurposeAndReachComeFromItsEffects()
    {
        using var strike = Ability("Strike", HeroAbilityCategory.Q, Area(3f, 2f), Buff(SkillTarget.Self, 0f));
        using var rally = Ability("Rally", HeroAbilityCategory.E, Buff(SkillTarget.AlliesInRadius, 6f));
        using var guard = Ability("Guard", HeroAbilityCategory.R, Buff(SkillTarget.OwnSquad, 0f));
        using var slow = Ability("Slow", HeroAbilityCategory.Ultimate, new SkillEvent { type = SkillEventType.ApplyModifier, target = SkillTarget.EnemiesInRadius, radius = 8f });
        Assert.That(HeroAISkills.PurposeOf(ref strike.Value, out float reach), Is.EqualTo(AISkillPurpose.Offensive));
        Assert.That(reach, Is.EqualTo(5f), "Centre distance plus radius.");
        Assert.That(HeroAISkills.PurposeOf(ref rally.Value, out reach), Is.EqualTo(AISkillPurpose.SupportAllies));
        Assert.That(reach, Is.EqualTo(6f));
        Assert.That(HeroAISkills.PurposeOf(ref guard.Value, out _), Is.EqualTo(AISkillPurpose.SupportSelf));
        Assert.That(HeroAISkills.PurposeOf(ref slow.Value, out _), Is.EqualTo(AISkillPurpose.Offensive), "Weakening enemies is offensive.");
    }

    [Test]
    public void WhenEachKindOfSkillIsWorthCasting()
    {
        Assert.That(HeroAISkills.ShouldCast(AISkillPurpose.Offensive, 1, 0, false, false, 0f, Tuning), Is.True, "One enemy hero is enough.");
        Assert.That(HeroAISkills.ShouldCast(AISkillPurpose.Offensive, 0, Tuning.minEnemyUnits - 1, true, false, 0f, Tuning), Is.False);
        Assert.That(HeroAISkills.ShouldCast(AISkillPurpose.Offensive, 0, Tuning.minEnemyUnits, false, false, 0f, Tuning), Is.True);
        Assert.That(HeroAISkills.ShouldCast(AISkillPurpose.SupportSelf, 0, 0, false, false, 0f, Tuning), Is.False, "Not while idle.");
        Assert.That(HeroAISkills.ShouldCast(AISkillPurpose.SupportSelf, 0, 0, true, false, 0f, Tuning), Is.True);
        Assert.That(HeroAISkills.ShouldCast(AISkillPurpose.SupportAllies, 0, 0, true, true, 0.1f, Tuning), Is.False, "Ally barely hurt.");
        Assert.That(HeroAISkills.ShouldCast(AISkillPurpose.SupportAllies, 0, 0, false, true, 0.5f, Tuning), Is.True);
        Assert.That(HeroAISkills.ShouldCast(AISkillPurpose.SupportAllies, 0, 0, true, false, 0.9f, Tuning), Is.False, "Ally out of reach.");
    }

    [Test]
    public void TheAllyScoreWeighsClosenessHealthLostAndHeroes()
    {
        var near = new AllyCandidate { distance = 1f };
        var far = new AllyCandidate { distance = 14f };
        var hurt = new AllyCandidate { distance = 10f, missingHealth = 0.6f };
        var hero = new AllyCandidate { distance = 1f, isHero = true };
        Assert.That(HeroAISkills.AllyScore(near, Tuning), Is.GreaterThan(HeroAISkills.AllyScore(far, Tuning)));
        Assert.That(HeroAISkills.AllyScore(hurt, Tuning), Is.GreaterThan(HeroAISkills.AllyScore(near, Tuning)), "Health lost weighs more than closeness.");
        Assert.That(HeroAISkills.AllyScore(hero, Tuning), Is.GreaterThan(HeroAISkills.AllyScore(near, Tuning)));
        Assert.That(HeroAISkills.AllyScore(new AllyCandidate { distance = Tuning.allySearchRadius + 1f }, Tuning), Is.LessThan(0f));
    }

    [Test]
    public void TheChosenAllyStaysUntilAnotherIsClearlyBetter()
    {
        var a = new Entity { Index = 1, Version = 1 };
        var b = new Entity { Index = 2, Version = 1 };
        var candidates = new[]
        {
            new AllyCandidate { entity = a, distance = 5f, missingHealth = 0.30f },
            new AllyCandidate { entity = b, distance = 5f, missingHealth = 0.35f },
        };
        Assert.That(HeroAISkills.PickAlly(candidates, Entity.Null, Tuning, out _), Is.EqualTo(b), "With no current one, the best.");
        Assert.That(HeroAISkills.PickAlly(candidates, a, Tuning, out _), Is.EqualTo(a), "A small edge does not take the place.");
        candidates[1].missingHealth = 0.6f;
        Assert.That(HeroAISkills.PickAlly(candidates, a, Tuning, out var picked), Is.EqualTo(b), "A clear one does.");
        Assert.That(picked.missingHealth, Is.EqualTo(0.6f));
        Assert.That(HeroAISkills.PickAlly(new AllyCandidate[0], a, Tuning, out _), Is.EqualTo(Entity.Null), "Gone: none.");
    }

    World _world;
    EntityManager _em;

    [SetUp]
    public void SetUp()
    {
        _world = new World("AI skills");
        _em = _world.EntityManager;
        _em.SetComponentData(_em.CreateEntity(typeof(MatchStateComponent)), new MatchStateComponent { currentState = MatchState.InBattle });
    }
    [TearDown] public void TearDown() => _world.Dispose();

    Entity Body(float3 position, Team team, bool hero, float health = 100f)
    {
        var e = _em.CreateEntity(typeof(TeamComponent));
        _em.AddComponentData(e, LocalTransform.FromPosition(position));
        _em.SetComponentData(e, new TeamComponent { value = team });
        if (hero) _em.AddComponentData(e, new HeroHealthComponent { currentHealth = health, maxHealth = 100f });
        else _em.AddComponentData(e, new HealthComponent { currentHealth = health, maxHealth = 100f });
        return e;
    }

    Entity AIHero(SkillSlot slot, BlobAssetReference<SkillBlob> skill)
    {
        var hero = Body(float3.zero, Team.TeamA, true);
        _em.AddComponent<HeroAITag>(hero);
        _em.AddComponent<HeroAIDecision>(hero);
        _em.AddComponentData(hero, new StaminaComponent { currentStamina = 100f, maxStamina = 100f });
        _em.AddComponent<SkillRequestComponent>(hero);
        _em.SetComponentEnabled<SkillRequestComponent>(hero, false);
        _em.AddBuffer<CooldownGroupElement>(hero);
        var buffer = _em.AddBuffer<HeroSkillElement>(hero);
        buffer.Add(new HeroSkillElement { slot = slot, skillId = skill.Value.id, skill = skill });
        _em.AddComponentObject(hero, new HeroAIBlackboard { selfIsAlive = true, selfTeam = Team.TeamA, lastThinkTime = 5.0 });
        return hero;
    }

    void Think()
    {
        _world.SetTime(new Unity.Core.TimeData(5.0, 0.1f));
        _world.GetOrCreateSystemManaged<HeroAISkillSystem>().Update();
    }

    [Test]
    public void AnAIHeroCastsItsOffensiveSkillAtAnEnemyHeroInReachAndTheRequestGoesThroughTheGate()
    {
        using var strike = Ability("AI Strike", HeroAbilityCategory.Q, Area(3f, 1f));
        var hero = AIHero(SkillSlot.Q, strike);
        var enemy = Body(new float3(10f, 0f, 0f), Team.TeamB, true);
        Think();
        Assert.That(_em.GetComponentData<HeroAIDecision>(hero).useSkill, Is.False, "Out of reach.");

        _em.SetComponentData(enemy, LocalTransform.FromPosition(new float3(3f, 0f, 0f)));
        Think();
        var decision = _em.GetComponentData<HeroAIDecision>(hero);
        Assert.That(decision.useSkill, Is.True);
        Assert.That(decision.skillSlot, Is.EqualTo(SkillSlot.Q));

        _world.GetOrCreateSystemManaged<HeroSkillRequestSystem>().Update();
        Assert.That(_em.IsComponentEnabled<SkillRequestComponent>(hero), Is.True);
        Assert.That(_em.GetComponentData<SkillRequestComponent>(hero).slot, Is.EqualTo(SkillSlot.Q));

        CooldownUtility.Start(_em.GetBuffer<CooldownGroupElement>(hero), strike.Value.cooldownGroup, 10f, 1f, 5.0);
        Think();
        Assert.That(_em.GetComponentData<HeroAIDecision>(hero).useSkill, Is.False, "Not asked again while cooling down.");
    }

    [Test]
    public void AnAIHeroSupportsTheHurtAllyItChoseAndKeepsIt()
    {
        using var rally = Ability("AI Rally", HeroAbilityCategory.E, Buff(SkillTarget.AlliesInRadius, 6f));
        var hero = AIHero(SkillSlot.E, rally);
        var fine = Body(new float3(2f, 0f, 0f), Team.TeamA, false, 95f);
        Think();
        Assert.That(_em.GetComponentData<HeroAISupportTarget>(hero).ally, Is.EqualTo(fine), "The only ally around.");
        Assert.That(_em.GetComponentData<HeroAIDecision>(hero).useSkill, Is.False, "Nobody needs it.");

        var hurt = Body(new float3(4f, 0f, 0f), Team.TeamA, true, 40f);
        Think();
        Assert.That(_em.GetComponentData<HeroAISupportTarget>(hero).ally, Is.EqualTo(hurt), "A hurt hero is clearly better.");
        var decision = _em.GetComponentData<HeroAIDecision>(hero);
        Assert.That(decision.useSkill, Is.True);
        Assert.That(decision.skillSlot, Is.EqualTo(SkillSlot.E));
    }
}
