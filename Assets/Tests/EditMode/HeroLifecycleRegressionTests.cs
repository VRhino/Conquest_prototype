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
            typeof(HeroLifeComponent), typeof(HeroSpawnComponent));
        em.SetComponentData(hero, new HeroHealthComponent { currentHealth = 0, maxHealth = 100 });
        em.SetComponentData(hero, new HeroLifeComponent { isAlive = true, respawnCooldown = 2 });
        em.SetComponentData(hero, new HeroSpawnComponent { hasSpawned = true });
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
}
