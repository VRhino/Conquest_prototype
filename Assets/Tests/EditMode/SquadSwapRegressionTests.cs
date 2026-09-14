using NUnit.Framework;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;

public class SquadSwapRegressionTests
{
    [Test]
    public void IncomingOrderCannotUnlockRetirement()
    {
        using var world = new World("Retirement order regression");
        var em = world.EntityManager;
        em.CreateEntity(typeof(MatchStateComponent));
        var squad = em.CreateEntity(typeof(SquadStateComponent), typeof(SquadInputComponent),
            typeof(SquadOwnerComponent), typeof(SquadResolvedOrderComponent));
        em.SetComponentData(squad, new SquadStateComponent { retreatTriggered = true,
            currentState = SquadFSMState.Retreating, transitionTo = SquadFSMState.Retreating });
        em.SetComponentData(squad, new SquadResolvedOrderComponent { hasNewOrder = true, order = SquadOrderType.FollowHero });
        world.GetOrCreateSystemManaged<SquadOrderSystem>().Update();
        Assert.That(em.GetComponentData<SquadStateComponent>(squad).transitionTo, Is.EqualTo(SquadFSMState.Retreating));
        Assert.That(em.GetComponentData<SquadResolvedOrderComponent>(squad).hasNewOrder, Is.False);
    }

    // 0: success; 1: missing definition; 2: no retreat point; 3: unavailable reserve;
    // 4: requested instance still retreating; 5: missing formation.
    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    public void InvalidReplacementDoesNotRetireOrChargeCooldown(int failure)
    {
        using var world = new World("Squad swap regression");
        var em = world.EntityManager;
        em.CreateEntity(typeof(MatchStateComponent));
        em.CreateEntity(typeof(SquadSpawnConfigComponent));
        using var builder = new BlobBuilder(Allocator.Temp);
        ref var root = ref builder.ConstructRoot<FormationLibraryBlob>();
        var formations = builder.Allocate(ref root.formations, 1);
        var grid = builder.Allocate(ref formations[0].gridPositions, 1);
        grid[0] = int2.zero;
        var blob = builder.CreateBlobAssetReference<FormationLibraryBlob>(Allocator.Persistent);
        try
        {
            var data = em.CreateEntity(typeof(SquadDataIDComponent), typeof(SquadDataComponent));
            em.SetComponentData(data, new SquadDataIDComponent { id = "new" });
            if (failure != 1)
                em.AddComponentData(data, new SquadDefinitionComponent
                { unitCount = 1, unitPrefab = em.CreateEntity(typeof(Prefab)), formationLibrary = failure == 5 ? default : blob });
            var old = em.CreateEntity(typeof(SquadStateComponent), typeof(SquadInstanceComponent), typeof(IsLocalSquadActive));
            em.SetComponentData(old, new SquadInstanceComponent { id = 1, initialUnitCount = 6 });
            em.AddBuffer<SquadUnitElement>(old).Add(new SquadUnitElement { Value = em.CreateEntity() });
            var hero = em.CreateEntity(typeof(TeamComponent), typeof(HeroSquadReference),
                typeof(HeroSquadSelectionComponent), typeof(HeroLifeComponent), typeof(HeroSpawnComponent),
                typeof(LocalTransform), typeof(SquadSwapExecuteTag));
            em.SetComponentData(hero, new HeroLifeComponent { isAlive = true });
            em.SetComponentData(hero, new HeroSpawnComponent { hasSpawned = true });
            em.SetComponentData(hero, new HeroSquadReference { squad = old });
            em.SetComponentData(hero, new SquadSwapExecuteTag { newSquadId = 2 });
            var reserve = em.AddBuffer<InactiveSquadElement>(hero);
            reserve.Add(new InactiveSquadElement { squadId = 2, baseSquadID = "new", aliveUnits = failure == 3 ? 0 : 1 });
            // Existing stale entry must be replaced, not duplicated.
            reserve.Add(new InactiveSquadElement { squadId = 1, baseSquadID = "stale" });
            em.AddBuffer<SquadIdMapElement>(hero).Add(new SquadIdMapElement { squadId = 1, baseSquadID = "remote-old" });
            if (failure != 2)
                em.AddComponentData(em.CreateEntity(), new SpawnPointComponent { isActive = true, teamID = 0 });
            if (failure == 4)
                em.AddComponentData(em.CreateEntity(), new SquadRetreatingFromSwapTag { squadId = 2, heroEntity = hero });
            world.GetOrCreateSystemManaged<SquadSwapExecutionSystem>().Update();
            Assert.That(em.HasComponent<SquadSwapExecuteTag>(hero), Is.False);
            if (failure != 0)
            {
                Assert.That(em.GetComponentData<HeroSquadReference>(hero).squad, Is.EqualTo(old));
                Assert.That(em.HasComponent<RetreatComponent>(old), Is.False);
                Assert.That(em.HasComponent<IsLocalSquadActive>(old), Is.True);
                Assert.That(em.HasComponent<SquadSwapCooldownComponent>(hero), Is.False);
                Assert.That(em.GetBuffer<InactiveSquadElement>(hero).Length, Is.EqualTo(2));
            }
            else
            {
                Assert.That(em.HasComponent<HeroSquadReference>(hero), Is.False);
                Assert.That(em.HasComponent<RetreatComponent>(old), Is.True);
                Assert.That(em.HasComponent<IsLocalSquadActive>(old), Is.False);
                Assert.That(em.HasComponent<SquadSwapCooldownComponent>(hero), Is.True);
                Assert.That(em.GetComponentData<HeroSquadSelectionComponent>(hero).squadDataEntity, Is.EqualTo(data));
                var entries = em.GetBuffer<InactiveSquadElement>(hero);
                Assert.That(entries.Length, Is.EqualTo(2));
                Assert.That(entries[1].baseSquadID.ToString(), Is.EqualTo("remote-old"));
                Assert.That(entries[1].totalUnits, Is.EqualTo(6));
                Assert.That(entries[1].aliveUnits, Is.EqualTo(1));
            }
        }
        finally { blob.Dispose(); }
    }

    [Test]
    public void RetreatWaitsForAllSurvivorsAtTheirSlotsAndPersistsCasualties()
    {
        using var world = new World("Retreat regression");
        var em = world.EntityManager;
        em.CreateEntity(typeof(MatchStateComponent));
        var hero = em.CreateEntity();
        em.AddBuffer<InactiveSquadElement>(hero).Add(new InactiveSquadElement { squadId = 1, totalUnits = 6 });
        var squad = em.CreateEntity(typeof(SquadStateComponent), typeof(RetreatComponent), typeof(SquadNavigationComponent));
        em.SetComponentData(squad, new SquadStateComponent { currentState = SquadFSMState.Retreating });
        em.SetComponentData(squad, new RetreatComponent { retreatDuration = 10 });
        em.AddComponentData(squad, new SquadRetreatingFromSwapTag { squadId = 1, heroEntity = hero });
        var leader = em.CreateEntity(typeof(LocalTransform), typeof(UnitTargetPositionComponent));
        em.SetComponentData(leader, LocalTransform.Identity);
        var follower = em.CreateEntity(typeof(LocalTransform), typeof(UnitTargetPositionComponent));
        var slot = new float3(3, 0, 0);
        em.SetComponentData(follower, LocalTransform.FromPosition(new float3(20, 0, 0)));
        em.SetComponentData(follower, new UnitTargetPositionComponent { position = slot });
        var units = em.AddBuffer<SquadUnitElement>(squad);
        units.Add(new SquadUnitElement { Value = leader });
        units.Add(new SquadUnitElement { Value = follower });
        var system = world.GetOrCreateSystemManaged<RetreatLogicSystem>();
        system.Update();
        Assert.That(em.Exists(squad), Is.True);
        em.SetComponentData(follower, LocalTransform.FromPosition(slot));
        system.Update();
        Assert.That(em.Exists(squad), Is.False);
        Assert.That(em.Exists(leader), Is.False);
        Assert.That(em.Exists(follower), Is.False);
        Assert.That(em.GetBuffer<InactiveSquadElement>(hero)[0].aliveUnits, Is.EqualTo(2));
        Assert.That(em.GetBuffer<InactiveSquadElement>(hero)[0].totalUnits, Is.EqualTo(6));
    }
}
