using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public class SquadControlRegressionTests
{
    World world;
    EntityManager em;

    [SetUp]
    public void SetUp()
    {
        world = new World("Squad control regression");
        em = world.EntityManager;
        em.CreateEntity(typeof(MatchStateComponent));
    }

    [TearDown]
    public void TearDown() => world.Dispose();

    Entity CreateSquad(bool local = false)
    {
        var hero = em.CreateEntity(typeof(LocalTransform));
        em.SetComponentData(hero, LocalTransform.Identity);
        if (local) em.AddComponent<IsLocalPlayer>(hero);
        var squad = em.CreateEntity(typeof(SquadOwnerComponent), typeof(SquadInputComponent),
            typeof(SquadPlayerOrderIntentComponent), typeof(SquadAIOrderIntentComponent),
            typeof(SquadCombatReactionIntentComponent), typeof(SquadResolvedOrderComponent),
            typeof(SquadStateComponent), typeof(FormationComponent));
        em.SetComponentData(squad, new SquadOwnerComponent { hero = hero });
        return squad;
    }

    [Test]
    public void HeroAIPublishesToResolverAndPreservesHoldDestination()
    {
        var squad = CreateSquad();
        var hero = em.GetComponentData<SquadOwnerComponent>(squad).hero;
        em.AddComponent<HeroAITag>(hero);
        em.AddComponentData(hero, new HeroLifeComponent { isAlive = true });
        em.AddComponentData(hero, new HeroStatsComponent { baseSpeed = 5 });
        em.AddComponentData(hero, new HeroSquadReference { squad = squad });
        var position = new float3(17, 2, 9);
        em.AddComponentData(hero, new HeroAIDecision
        { hasNewSquadOrder = true, squadOrder = SquadOrderType.HoldPosition, squadOrderPosition = position });
        world.GetOrCreateSystemManaged<HeroAIExecutionSystem>().Update();
        world.GetOrCreateSystemManaged<OrderResolutionSystem>().Update();
        var resolved = em.GetComponentData<SquadResolvedOrderComponent>(squad);
        Assert.That(resolved.order, Is.EqualTo(SquadOrderType.HoldPosition));
        Assert.That(resolved.holdPosition, Is.EqualTo(position));
        Assert.That(resolved.hasNewOrder, Is.True);
        Assert.That(em.GetComponentData<HeroAIDecision>(hero).hasNewSquadOrder, Is.False);
    }

    [Test]
    public void SameAIOrderWithNewPositionOrTargetIsReissuedButUnchangedOrderIsNot()
    {
        var squad = CreateSquad();
        var intent = new SquadAIOrderIntentComponent { suggestedOrder = SquadOrderType.HoldPosition };
        em.SetComponentData(squad, intent);
        var resolver = world.GetOrCreateSystemManaged<OrderResolutionSystem>();
        resolver.Update();
        resolver.Update();
        Assert.That(em.GetComponentData<SquadResolvedOrderComponent>(squad).hasNewOrder, Is.False);
        intent.holdPosition = new float3(5, 0, 8);
        em.SetComponentData(squad, intent);
        resolver.Update();
        Assert.That(em.GetComponentData<SquadResolvedOrderComponent>(squad).hasNewOrder, Is.True);
        intent.targetEntity = em.CreateEntity();
        em.SetComponentData(squad, intent);
        resolver.Update();
        Assert.That(em.GetComponentData<SquadResolvedOrderComponent>(squad).targetEntity, Is.EqualTo(intent.targetEntity));
        Assert.That(em.GetComponentData<SquadResolvedOrderComponent>(squad).hasNewOrder, Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CombatReactionDoesNotReplaceHoldOrder(bool local)
    {
        var squad = CreateSquad(local);
        var position = new float3(4, 0, 12);
        em.SetComponentData(squad, new SquadInputComponent { hasNewOrder = true });
        em.SetComponentData(squad, new SquadPlayerOrderIntentComponent
        { orderType = SquadOrderType.HoldPosition, holdPosition = position });
        em.SetComponentData(squad, new SquadAIOrderIntentComponent
        { suggestedOrder = SquadOrderType.HoldPosition, holdPosition = position });
        em.SetComponentData(squad, new SquadCombatReactionIntentComponent { reactToEnemy = true });
        world.GetOrCreateSystemManaged<OrderResolutionSystem>().Update();
        var resolved = em.GetComponentData<SquadResolvedOrderComponent>(squad);
        Assert.That(resolved.order, Is.EqualTo(SquadOrderType.HoldPosition));
        Assert.That(resolved.holdPosition, Is.EqualTo(position));
    }

    [Test]
    public void AIRequestSurvivesTemporaryCombatOverride()
    {
        var squad = CreateSquad();
        em.SetComponentData(squad, new SquadAIOrderIntentComponent { suggestedOrder = SquadOrderType.FollowHero });
        em.SetComponentData(squad, new SquadCombatReactionIntentComponent { reactToEnemy = true });
        var resolver = world.GetOrCreateSystemManaged<OrderResolutionSystem>();
        resolver.Update();
        Assert.That(em.GetComponentData<SquadResolvedOrderComponent>(squad).order, Is.EqualTo(SquadOrderType.Attack));
        em.SetComponentData(squad, new SquadCombatReactionIntentComponent());
        resolver.Update();
        var resolved = em.GetComponentData<SquadResolvedOrderComponent>(squad);
        Assert.That(resolved.order, Is.EqualTo(SquadOrderType.FollowHero));
        Assert.That(resolved.hasNewOrder, Is.True);
    }

    [Test]
    public void HoldComponentIsAddedAndRemovedBeforeDownstreamSystems()
    {
        var squad = CreateSquad();
        var position = new float3(3, 0, 6);
        em.SetComponentData(squad, new SquadResolvedOrderComponent
        { order = SquadOrderType.HoldPosition, holdPosition = position, hasNewOrder = true });
        var orders = world.GetOrCreateSystemManaged<SquadOrderSystem>();
        orders.Update();
        Assert.That(em.GetComponentData<SquadHoldPositionComponent>(squad).holdCenter, Is.EqualTo(position));
        em.SetComponentData(squad, new SquadResolvedOrderComponent
        { order = SquadOrderType.FollowHero, hasNewOrder = true });
        orders.Update();
        Assert.That(em.HasComponent<SquadHoldPositionComponent>(squad), Is.False);
    }

    [Test]
    public void HoldingAnchorStaysFixedDuringCombatButNotRetreat()
    {
        var squad = CreateSquad();
        em.AddComponentData(squad, new SquadFormationAnchorComponent());
        em.AddComponent<SquadAnchorMovingTag>(squad);
        em.AddComponentData(squad, new HeroWorldPositionComponent
        { position = new float3(100, 0, 100), rotation = quaternion.identity });
        em.AddComponentData(squad, new SquadDataComponent { isRangedUnit = true });
        var hold = new SquadHoldPositionComponent { holdCenter = new float3(3, 0, 4), holdRotation = quaternion.identity };
        em.AddComponentData(squad, hold);
        var state = new SquadStateComponent { currentOrder = SquadOrderType.HoldPosition, currentState = SquadFSMState.InCombat };
        em.SetComponentData(squad, state);
        var anchor = world.GetOrCreateSystemManaged<SquadAnchorSystem>();
        anchor.Update();
        Assert.That(em.GetComponentData<SquadFormationAnchorComponent>(squad).position, Is.EqualTo(hold.holdCenter));
        var retreat = new float3(20, 0, 20);
        em.AddComponentData(squad, new RetreatComponent { retreatTarget = retreat });
        state.currentState = SquadFSMState.Retreating;
        em.SetComponentData(squad, state);
        anchor.Update();
        Assert.That(em.GetComponentData<SquadFormationAnchorComponent>(squad).position, Is.EqualTo(retreat));
        Assert.That(FormationPositionCalculator.GetSquadCenter(state, hold, retreat), Is.EqualTo(retreat));
    }

    [Test]
    public void FollowAnchorUsesSafeDefaultsWhenSpawnConfigIsAbsent()
    {
        var squad = CreateSquad();
        em.AddComponentData(squad, new SquadFormationAnchorComponent());
        em.AddComponent<SquadAnchorMovingTag>(squad);
        em.AddComponentData(squad, new SquadDataComponent());
        em.AddComponentData(squad, new HeroWorldPositionComponent
        {
            position = new float3(10, 0, 20),
            rotation = quaternion.identity
        });
        em.SetComponentData(squad, new SquadStateComponent
        {
            currentOrder = SquadOrderType.FollowHero,
            currentState = SquadFSMState.FollowingHero
        });

        world.GetOrCreateSystemManaged<SquadAnchorSystem>().Update();

        Assert.That(em.GetComponentData<SquadFormationAnchorComponent>(squad).position,
            Is.EqualTo(new float3(10, 0, 22)));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void FormationIsCommittedWithSlotsNotByOrderApplication(bool movementOrder)
    {
        var squad = CreateSquad();
        var unit = em.CreateEntity(typeof(UnitGridSlotComponent), typeof(UnitSpacingComponent));
        em.SetComponentData(unit, new UnitGridSlotComponent { slotIndex = 99 });
        em.AddBuffer<SquadUnitElement>(squad).Add(new SquadUnitElement { Value = unit });
        em.AddComponent<SquadActiveFormationComponent>(squad);
        em.AddComponentData(squad, new SquadFormationAnchorComponent { rotation = quaternion.identity });
        using var builder = new BlobBuilder(Allocator.Temp);
        ref var library = ref builder.ConstructRoot<FormationLibraryBlob>();
        var formations = builder.Allocate(ref library.formations, 1);
        formations[0].formationType = FormationType.Column;
        var grid = builder.Allocate(ref formations[0].gridPositions, 1);
        grid[0] = new int2(2, 3);
        var blob = builder.CreateBlobAssetReference<FormationLibraryBlob>(Allocator.Persistent);
        try
        {
            em.AddComponentData(squad, new SquadDefinitionComponent { formationLibrary = blob });
            em.SetComponentData(squad, new SquadInputComponent { desiredFormation = FormationType.Column });
            em.SetComponentData(squad, new SquadResolvedOrderComponent
            { order = SquadOrderType.FollowHero, hasNewOrder = movementOrder });
            world.GetOrCreateSystemManaged<SquadOrderSystem>().Update();
            Assert.That(em.GetComponentData<FormationComponent>(squad).currentFormation, Is.EqualTo(FormationType.Line));
            world.GetOrCreateSystemManaged<FormationSystem>().Update();
            Assert.That(em.GetComponentData<FormationComponent>(squad).currentFormation, Is.EqualTo(FormationType.Column));
            Assert.That(em.GetComponentData<SquadActiveFormationComponent>(squad).currentFormation, Is.EqualTo(FormationType.Column));
            Assert.That(em.GetComponentData<UnitGridSlotComponent>(unit).gridPosition, Is.EqualTo(new int2(2, 3)));
            Assert.That(em.GetComponentData<UnitGridSlotComponent>(unit).slotIndex, Is.Zero);
            Assert.That(em.GetComponentData<UnitSpacingComponent>(unit).Slot, Is.EqualTo(new int2(2, 3)));
            Assert.That(em.GetComponentData<SquadStateComponent>(squad).formationChangeCooldown, Is.GreaterThan(0));
        }
        finally { blob.Dispose(); }
    }

    [TestCase(SquadOrderType.HoldPosition, 1f, false)]
    [TestCase(SquadOrderType.HoldPosition, 20f, false)]
    [TestCase(SquadOrderType.FollowHero, 1f, true)]
    [TestCase(SquadOrderType.FollowHero, 20f, false)]
    [TestCase(SquadOrderType.Attack, 20f, true)]
    public void PursuitDependsOnOrderAndLeashNotCombatState(SquadOrderType order, float distance, bool expected)
    {
        Assert.That(UnitNavMeshSystem.CanPursueTarget(order, float3.zero, new float3(distance, 0, 0), 6), Is.EqualTo(expected));
    }

}
