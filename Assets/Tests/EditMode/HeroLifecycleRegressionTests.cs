using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public class HeroLifecycleRegressionTests
{
    [Test]
    public void SpawnPublishesOneRevisionOnlyAfterFindingAValidPoint()
    {
        using var world = new World("Hero spawn revision regression");
        var em = world.EntityManager;
        var hero = em.CreateEntity(typeof(IsLocalPlayer), typeof(LocalTransform),
            typeof(HeroLifeComponent), typeof(HeroSpawnComponent), typeof(TeamComponent));
        em.SetComponentData(hero, LocalTransform.Identity);
        em.SetComponentData(hero, new HeroLifeComponent { isAlive = true });
        em.SetComponentData(hero, new TeamComponent { value = (Team)1 });
        var spawn = world.GetOrCreateSystemManaged<HeroSpawnSystem>();
        spawn.Update();
        Assert.That(em.GetComponentData<HeroSpawnComponent>(hero).positionRevision, Is.Zero);
        Assert.That(em.GetComponentData<HeroSpawnComponent>(hero).hasSpawned, Is.False);
        var point = em.CreateEntity(typeof(SpawnPointComponent));
        em.SetComponentData(point, new SpawnPointComponent
        { spawnID = 0, teamID = 1, isActive = true, position = new float3(9, 0, 7) });
        spawn.Update();
        var pose = em.GetComponentData<HeroSpawnComponent>(hero);
        Assert.That(pose.hasSpawned, Is.True);
        Assert.That(pose.positionRevision, Is.EqualTo(1));
        Assert.That(em.GetComponentData<LocalTransform>(hero).Position, Is.EqualTo(pose.spawnPosition));
        spawn.Update();
        Assert.That(em.GetComponentData<HeroSpawnComponent>(hero).positionRevision, Is.EqualTo(1));
        pose.hasSpawned = false;
        em.SetComponentData(hero, pose);
        spawn.Update();
        Assert.That(em.GetComponentData<HeroSpawnComponent>(hero).positionRevision, Is.EqualTo(2));
    }

    [Test]
    public void CanonicalHeroHealthTriggersDeathThenRequestsRespawn()
    {
        using var world = new World("Hero lifecycle regression");
        var em = world.EntityManager;
        em.CreateEntity(typeof(MatchStateComponent));
        var hero = em.CreateEntity(typeof(IsLocalPlayer), typeof(HeroHealthComponent),
            typeof(HeroLifeComponent), typeof(HeroSpawnComponent), typeof(IsDeadComponent),
            typeof(LocalTransform), typeof(TeamComponent));
        em.SetComponentData(hero, new HeroHealthComponent { currentHealth = 0, maxHealth = 100 });
        em.SetComponentData(hero, new HeroLifeComponent { isAlive = true, respawnCooldown = 2 });
        em.SetComponentData(hero, new HeroSpawnComponent { hasSpawned = true });
        em.SetComponentData(hero, LocalTransform.Identity);
        em.SetComponentData(hero, new TeamComponent { value = (Team)1 });
        var system = world.GetOrCreateSystemManaged<HeroRespawnSystem>();
        world.SetTime(new Unity.Core.TimeData(0, 1));
        system.Update();
        Assert.That(em.GetComponentData<HeroLifeComponent>(hero).isAlive, Is.False);
        Assert.That(em.GetComponentData<HeroLifeComponent>(hero).deathTimer, Is.EqualTo(2));
        system.Update();
        Assert.That(em.GetComponentData<HeroLifeComponent>(hero).isAlive, Is.False);
        system.Update();
        Assert.That(em.GetComponentData<HeroLifeComponent>(hero).isAlive, Is.True);
        Assert.That(em.GetComponentData<HeroHealthComponent>(hero).currentHealth, Is.EqualTo(100));
        Assert.That(em.GetComponentData<HeroSpawnComponent>(hero).hasSpawned, Is.False);
        Assert.That(em.HasComponent<IsDeadComponent>(hero), Is.True);
        var point = em.CreateEntity(typeof(SpawnPointComponent));
        em.SetComponentData(point, new SpawnPointComponent
        { spawnID = 0, teamID = 1, isActive = true, position = new float3(9, 0, 7) });
        world.GetOrCreateSystemManaged<HeroSpawnSystem>().Update();
        Assert.That(em.GetComponentData<HeroSpawnComponent>(hero).hasSpawned, Is.True);
        Assert.That(em.HasComponent<IsDeadComponent>(hero), Is.False);
    }

    [Test]
    public void RemoteHeroWithZeroHealthStopsActingAndDoesNotRespawn()
    {
        using var world = new World("Remote hero death regression");
        var em = world.EntityManager;
        em.CreateEntity(typeof(MatchStateComponent));
        var hero = em.CreateEntity(typeof(HeroHealthComponent),
            typeof(HeroLifeComponent), typeof(HeroSpawnComponent), typeof(HeroAITag),
            typeof(IsDeadComponent));
        em.SetComponentData(hero, new HeroHealthComponent { currentHealth = 0, maxHealth = 100 });
        em.SetComponentData(hero, new HeroLifeComponent { isAlive = true, respawnCooldown = 2 });
        em.SetComponentData(hero, new HeroSpawnComponent { hasSpawned = true });

        var system = world.GetOrCreateSystemManaged<HeroRespawnSystem>();
        world.SetTime(new Unity.Core.TimeData(0, 1));
        system.Update();
        Assert.That(em.GetComponentData<HeroLifeComponent>(hero).isAlive, Is.False);
        system.Update();
        system.Update();
        Assert.That(em.GetComponentData<HeroLifeComponent>(hero).isAlive, Is.False);
        Assert.That(em.HasComponent<IsDeadComponent>(hero), Is.True);
        Assert.That(em.GetComponentData<HeroSpawnComponent>(hero).hasSpawned, Is.True);
    }

    [Test]
    public void DeadHeroCannotKeepAnActiveAttackHitbox()
    {
        using var world = new World("Dead hero attack regression");
        var em = world.EntityManager;
        em.SetComponentData(em.CreateEntity(typeof(MatchStateComponent)),
            new MatchStateComponent { currentState = MatchState.InBattle });
        var combat = new HeroCombatComponent { isAttacking = true, strikeWindowStart = 0.1f, strikeWindowDuration = 0.5f, attackAnimationDuration = 1f };
        using var basic = SkillBlobBuilder.BuildBasicAttack(combat, 0f, 0f);
        var hero = SkillHero(em, basic, combat, alive: false);
        em.SetComponentData(hero, new SkillInstanceComponent { active = true, phase = 1, phaseTime = 0.25f, hitboxOpen = true });
        em.SetComponentEnabled<WeaponHitboxActiveTag>(hero, true);

        world.GetOrCreateSystemManaged<SkillExecutionSystem>().Update();

        Assert.That(em.GetComponentData<HeroCombatComponent>(hero).isAttacking, Is.False);
        Assert.That(em.IsComponentEnabled<WeaponHitboxActiveTag>(hero), Is.False);
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public void DeadOrUnpositionedHeroCannotRetainMovement(bool alive, bool spawned)
    {
        using var world = new World("Hero movement regression");
        var em = world.EntityManager;
        var hero = em.CreateEntity(typeof(IsLocalPlayer), typeof(HeroInputComponent),
            typeof(HeroStatsComponent), typeof(StaminaComponent), typeof(HeroLifeComponent),
            typeof(HeroSpawnComponent), typeof(HeroMoveIntent));
        em.SetComponentData(hero, new HeroLifeComponent { isAlive = alive });
        em.SetComponentData(hero, new HeroSpawnComponent { hasSpawned = spawned });
        em.SetComponentData(hero, new HeroMoveIntent { Direction = math.forward(), Speed = 10 });
        world.GetOrCreateSystemManaged<HeroMovementSystem>().Update();
        var intent = em.GetComponentData<HeroMoveIntent>(hero);
        Assert.That(intent.Speed, Is.Zero);
        Assert.That(intent.Direction, Is.EqualTo(float3.zero));
    }

    [Test]
    public void HeldAttackChargesStaminaOnlyWhenAttackStarts()
    {
        using var world = new World("Hero attack stamina regression");
        var em = world.EntityManager;
        em.SetComponentData(em.CreateEntity(typeof(MatchStateComponent)),
            new MatchStateComponent { currentState = MatchState.InBattle });
        em.CreateSingleton(new HeroGameplayConfigComponent { attackStaminaCost = 15f, attackCooldown = 1f });
        var combat = new HeroCombatComponent { attackAnimationDuration = 2f, strikeWindowStart = 1f, strikeWindowDuration = 0.5f };
        using var basic = SkillBlobBuilder.BuildBasicAttack(combat, 15f, 1f);
        var hero = SkillHero(em, basic, combat, alive: true);
        em.AddComponent<IsLocalPlayer>(hero);
        em.AddComponentData(hero, new HeroInputComponent { IsAttackPressed = true });
        em.AddComponent<HeroStatsComponent>(hero);
        em.SetComponentData(hero, new StaminaComponent
        { currentStamina = 100f, maxStamina = 100f, regenRate = 10f });
        world.SetTime(new Unity.Core.TimeData(0, 0.1f));

        var request = world.GetOrCreateSystemManaged<HeroSkillRequestSystem>();
        var attack = world.GetOrCreateSystemManaged<SkillExecutionSystem>();
        var stamina = world.GetOrCreateSystemManaged<HeroStaminaSystem>();
        request.Update();
        attack.Update();
        stamina.Update();
        Assert.That(em.GetComponentData<StaminaComponent>(hero).currentStamina,
            Is.EqualTo(85f).Within(0.001f));
        request.Update();
        attack.Update();
        stamina.Update();
        Assert.That(em.GetComponentData<StaminaComponent>(hero).currentStamina,
            Is.EqualTo(85f).Within(0.001f));
    }

    [Test]
    public void SprintCostsStaminaOnlyWhileMovingAndAiRegenerates()
    {
        using var world = new World("Hero sprint stamina regression");
        var em = world.EntityManager;
        var config = em.CreateEntity(typeof(HeroGameplayConfigComponent));
        em.SetComponentData(config, new HeroGameplayConfigComponent
        { sprintStaminaCostPerSecond = 10f, exhaustionRecoveryThreshold = 0.2f });
        var hero = em.CreateEntity(typeof(HeroAITag), typeof(HeroStatsComponent),
            typeof(HeroLifeComponent), typeof(StaminaComponent), typeof(HeroMoveIntent));
        em.SetComponentData(hero, new HeroStatsComponent { baseSpeed = 4f });
        em.SetComponentData(hero, new HeroLifeComponent { isAlive = true });
        em.SetComponentData(hero, new StaminaComponent
        { currentStamina = 50f, maxStamina = 100f, regenRate = 5f });
        em.SetComponentData(hero, new HeroMoveIntent { Direction = math.forward(), Speed = 8f });
        world.SetTime(new Unity.Core.TimeData(0, 1f));
        var stamina = world.GetOrCreateSystemManaged<HeroStaminaSystem>();
        stamina.Update();
        Assert.That(em.GetComponentData<StaminaComponent>(hero).currentStamina,
            Is.EqualTo(40f).Within(0.001f));
        em.SetComponentData(hero, new HeroMoveIntent { Direction = float3.zero, Speed = 8f });
        stamina.Update();
        Assert.That(em.GetComponentData<StaminaComponent>(hero).currentStamina,
            Is.EqualTo(45f).Within(0.001f));
    }

    /// <summary>A hero with only its basic attack, for the skill lifecycle tests.</summary>
    static Entity SkillHero(EntityManager em, BlobAssetReference<SkillBlob> basic, HeroCombatComponent combat, bool alive)
    {
        var hero = em.CreateEntity(typeof(HeroLifeComponent), typeof(HeroCombatComponent), typeof(StaminaComponent),
            typeof(HeroAnimationComponent), typeof(WeaponHitboxActiveTag), typeof(SkillInstanceComponent),
            typeof(SkillRequestComponent));
        em.SetComponentData(hero, new HeroLifeComponent { isAlive = alive });
        em.SetComponentData(hero, combat);
        em.SetComponentEnabled<WeaponHitboxActiveTag>(hero, false);
        em.SetComponentEnabled<SkillRequestComponent>(hero, false);
        em.AddBuffer<CooldownGroupElement>(hero);
        em.AddBuffer<HeroSkillElement>(hero).Add(new HeroSkillElement
        { slot = SkillSlot.Basic, skillId = basic.Value.id, skill = basic });
        return hero;
    }
}
