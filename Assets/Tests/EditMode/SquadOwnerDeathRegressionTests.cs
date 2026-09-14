using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public class SquadOwnerDeathRegressionTests
{
    World world;
    EntityManager em;
    Entity hero, squad, unit;
    BlobAssetReference<FormationLibraryBlob> blob;

    [SetUp]
    public void SetUp()
    {
        world = new World("Owner death regression");
        em = world.EntityManager;
        em.CreateEntity(typeof(MatchStateComponent));
        em.AddComponentData(em.CreateEntity(), new SquadSpawnConfigComponent
        { ownerDeathRetreatDuration = 5, retreatArrivalThreshold = 0.5f });
        using var builder = new BlobBuilder(Allocator.Temp);
        ref var root = ref builder.ConstructRoot<FormationLibraryBlob>();
        var forms = builder.Allocate(ref root.formations, 1);
        var positions = builder.Allocate(ref forms[0].gridPositions, 3);
        for (int i = 0; i < 3; i++) positions[i] = new int2(i, 0);
        blob = builder.CreateBlobAssetReference<FormationLibraryBlob>(Allocator.Persistent);
        var definition = em.CreateEntity(typeof(SquadDataComponent), typeof(SquadDefinitionComponent), typeof(SquadDataIDComponent));
        em.SetComponentData(definition, new SquadDefinitionComponent { formationLibrary = blob, unitCount = 3 });
        em.SetComponentData(definition, new SquadDataComponent { baseHealth = 100 });
        em.SetComponentData(definition, new SquadDataIDComponent { id = "owner-squad" });
        hero = em.CreateEntity(typeof(HeroLifeComponent), typeof(HeroSpawnComponent), typeof(HeroSquadSelectionComponent),
            typeof(LocalTransform), typeof(TeamComponent), typeof(HeroSquadReference), typeof(IsLocalPlayer));
        em.SetComponentData(hero, LocalTransform.Identity);
        em.SetComponentData(hero, new HeroSpawnComponent { hasSpawned = true });
        em.SetComponentData(hero, new HeroSquadSelectionComponent { squadDataEntity = definition, instanceId = 7 });
        squad = em.CreateEntity(typeof(SquadOwnerComponent), typeof(TeamComponent), typeof(SquadStateComponent),
            typeof(SquadInstanceComponent), typeof(IsLocalSquadActive), typeof(SquadAIComponent),
            typeof(SquadFSMComponent), typeof(SquadPlayerOrderIntentComponent));
        em.SetComponentData(squad, new SquadOwnerComponent { hero = hero });
        em.SetComponentData(squad, new SquadInstanceComponent { id = 7, initialUnitCount = 3 });
        em.SetComponentData(hero, new HeroSquadReference { squad = squad });
        unit = em.CreateEntity(typeof(LocalTransform), typeof(UnitTargetPositionComponent));
        em.SetComponentData(unit, LocalTransform.FromPosition(new float3(100, 0, 0)));
        em.AddBuffer<SquadUnitElement>(squad).Add(new SquadUnitElement { Value = unit });
    }

    [TearDown]
    public void TearDown() { world.Dispose(); blob.Dispose(); }

    void AddPoint() => em.AddComponentData(em.CreateEntity(), new SpawnPointComponent { isActive = true });
    void Retire() => world.GetOrCreateSystemManaged<SquadOwnerDeathRetreatSystem>().Update();

    [Test]
    public void LivingOwnerDoesNotRetireAndDeathDoesNotReplaceSwapRetirement()
    {
        AddPoint();
        em.SetComponentData(hero, new HeroLifeComponent { isAlive = true });
        Retire();
        Assert.That(em.GetComponentData<SquadStateComponent>(squad).lastOwnerAlive, Is.True);
        Assert.That(em.HasComponent<RetreatComponent>(squad), Is.False);
        em.AddComponentData(squad, new RetreatComponent { retreatTimer = 2, retreatDuration = 5 });
        em.AddComponentData(squad, new SquadRetreatingFromSwapTag { heroEntity = hero, squadId = 7 });
        em.SetComponentData(hero, new HeroLifeComponent { isAlive = false });
        Retire();
        Assert.That(em.GetComponentData<RetreatComponent>(squad).retreatTimer, Is.EqualTo(2));
        Assert.That(em.HasComponent<SquadOwnerDeathRetreatComponent>(squad), Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RetirementPreservesSurvivorsAndBlocksDuplicateSpawn(bool revivesEarly)
    {
        AddPoint();
        Retire();
        Assert.That(em.HasComponent<RetreatComponent>(squad), Is.True);
        Assert.That(em.GetComponentData<SquadStateComponent>(squad).lastOwnerAlive, Is.False);
        world.GetOrCreateSystemManaged<SquadStatusUpdateSystem>().Update();
        Assert.That(em.HasComponent<IsLocalSquadActive>(squad), Is.False);
        var retreat = em.GetComponentData<RetreatComponent>(squad);
        retreat.retreatTimer = 2;
        em.SetComponentData(squad, retreat);
        em.SetComponentData(hero, new HeroLifeComponent { isAlive = revivesEarly });
        Retire();
        Assert.That(em.GetComponentData<RetreatComponent>(squad).retreatTimer, Is.EqualTo(2));
        var spawner = world.GetOrCreateSystemManaged<SquadSpawningSystem>();
        spawner.Update();
        Assert.That(em.GetComponentData<HeroSquadReference>(hero).squad, Is.EqualTo(squad));
        em.SetComponentData(unit, LocalTransform.Identity);
        world.GetOrCreateSystemManaged<RetreatLogicSystem>().Update();
        Assert.That(em.Exists(squad), Is.False);
        Assert.That(em.HasComponent<HeroSquadReference>(hero), Is.False);
        var reserve = em.GetBuffer<InactiveSquadElement>(hero)[0];
        Assert.That(reserve.aliveUnits, Is.EqualTo(1));
        Assert.That(reserve.totalUnits, Is.EqualTo(3));
        Assert.That(reserve.baseSquadID.ToString(), Is.EqualTo("owner-squad"));
        spawner.Update();
        Assert.That(em.HasComponent<HeroSquadReference>(hero), Is.EqualTo(revivesEarly));
        if (!revivesEarly)
        {
            em.SetComponentData(hero, new HeroLifeComponent { isAlive = true });
            spawner.Update();
        }
        var replacement = em.GetComponentData<HeroSquadReference>(hero).squad;
        Assert.That(em.GetBuffer<SquadUnitElement>(replacement).Length, Is.EqualTo(1));
    }

    [Test]
    public void PendingRetirementSurvivesRespawnUntilAlliedPointExists()
    {
        var originalAnchor = new SquadFormationAnchorComponent
        { position = new float3(12, 0, 8), rotation = quaternion.RotateY(1) };
        em.AddComponentData(squad, originalAnchor);
        em.AddComponent<SquadAnchorMovingTag>(squad);
        em.AddComponent<SquadDataComponent>(squad);
        em.AddComponentData(squad, new HeroWorldPositionComponent
        { position = new float3(300, 0, 100), rotation = quaternion.identity });
        Retire();
        Assert.That(em.HasComponent<SquadOwnerDeathRetreatComponent>(squad), Is.True);
        Assert.That(em.HasComponent<RetreatComponent>(squad), Is.False);
        em.SetComponentData(hero, new HeroLifeComponent { isAlive = true });
        Retire();
        Assert.That(em.HasComponent<RetreatComponent>(squad), Is.False);
        world.GetOrCreateSystemManaged<SquadAnchorSystem>().Update();
        Assert.That(em.GetComponentData<SquadFormationAnchorComponent>(squad).position, Is.EqualTo(originalAnchor.position));
        Assert.That(em.GetComponentData<SquadFormationAnchorComponent>(squad).rotation, Is.EqualTo(originalAnchor.rotation));
        AddPoint();
        Retire();
        Assert.That(em.HasComponent<RetreatComponent>(squad), Is.True);
    }

    [Test]
    public void LastCasualtyCannotDivertRetirementToKOOrRegenerateAnEliminatedSquad()
    {
        AddPoint();
        Retire();
        em.DestroyEntity(unit);
        em.GetBuffer<SquadUnitElement>(squad).Clear();
        world.GetOrCreateSystemManaged<SquadFSMSystem>().Update();
        Assert.That(em.GetComponentData<SquadStateComponent>(squad).currentState, Is.EqualTo(SquadFSMState.Retreating));
        world.GetOrCreateSystemManaged<RetreatLogicSystem>().Update();
        Assert.That(em.GetBuffer<InactiveSquadElement>(hero)[0].isEliminated, Is.True);
        em.SetComponentData(hero, new HeroLifeComponent { isAlive = true });
        world.GetOrCreateSystemManaged<SquadSpawningSystem>().Update();
        Assert.That(em.HasComponent<HeroSquadReference>(hero), Is.False);
    }

    [Test]
    public void CleanupDoesNotRemoveANewerSquadReference()
    {
        AddPoint();
        Retire();
        var newer = em.CreateEntity();
        em.SetComponentData(hero, new HeroSquadReference { squad = newer });
        em.SetComponentData(unit, LocalTransform.Identity);
        world.GetOrCreateSystemManaged<RetreatLogicSystem>().Update();
        Assert.That(em.GetComponentData<HeroSquadReference>(hero).squad, Is.EqualTo(newer));
    }
}
