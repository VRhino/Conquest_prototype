using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>Formation changes by tag instead of visiting every squad each frame (Aprendizajes_Referencia.md L3-06).</summary>
public class FormationChangePendingTests
{
    World world;
    EntityManager em;
    BlobAssetReference<FormationLibraryBlob> blob;

    [SetUp]
    public void SetUp()
    {
        world = new World("Formation change pending");
        em = world.EntityManager;
        em.SetComponentData(em.CreateEntity(typeof(MatchStateComponent)),
            new MatchStateComponent { currentState = MatchState.InBattle });
        blob = TestFormationLibrary.FromCells(FormationType.Column, 1f, new int2(0, 0), new int2(1, 0));
    }

    [TearDown]
    public void TearDown() { world.Dispose(); blob.Dispose(); }

    Entity CreateLocalSquad(out Entity unit)
    {
        var hero = em.CreateEntity(typeof(LocalTransform), typeof(IsLocalPlayer));
        var squad = em.CreateEntity(typeof(SquadOwnerComponent), typeof(SquadInputComponent),
            typeof(SquadPlayerOrderIntentComponent), typeof(SquadAIOrderIntentComponent),
            typeof(SquadCombatReactionIntentComponent), typeof(SquadResolvedOrderComponent),
            typeof(SquadStateComponent), typeof(FormationComponent), typeof(FormationChangePending));
        em.SetComponentData(squad, new SquadOwnerComponent { hero = hero });
        em.AddComponentData(squad, new SquadDefinitionComponent { formationLibrary = blob });
        em.AddComponentData(squad, new SquadFormationAnchorComponent { rotation = quaternion.identity });
        unit = em.CreateEntity(typeof(UnitGridSlotComponent));
        em.AddBuffer<SquadUnitElement>(squad).Add(new SquadUnitElement { Value = unit });
        em.SetComponentEnabled<FormationChangePending>(squad, false);
        return squad;
    }

    FormationType Current(Entity squad) => em.GetComponentData<FormationComponent>(squad).currentFormation;

    void Frame(double time)
    {
        world.SetTime(new TimeData(time, 0.02f));
        world.GetOrCreateSystemManaged<OrderResolutionSystem>().Update();
        world.GetOrCreateSystemManaged<FormationSystem>().Update();
    }

    [Test]
    public void ASquadWithoutTheTagIsNotVisited()
    {
        var squad = CreateLocalSquad(out _);
        em.SetComponentData(squad, new SquadResolvedOrderComponent { formation = FormationType.Column });
        world.GetOrCreateSystemManaged<FormationSystem>().Update();
        Assert.That(Current(squad), Is.EqualTo(FormationType.Line), "No tag, no visit.");
    }

    [Test]
    public void TheRequestTurnsTheTagOnAndTheCommitTurnsItOff()
    {
        var squad = CreateLocalSquad(out _);
        em.SetComponentData(squad, new SquadPlayerOrderIntentComponent { desiredFormation = FormationType.Column });
        Frame(1.0);
        Assert.That(Current(squad), Is.EqualTo(FormationType.Column));
        Assert.That(em.IsComponentEnabled<FormationChangePending>(squad), Is.False, "Nothing left pending.");
        Frame(1.02);
        Assert.That(em.IsComponentEnabled<FormationChangePending>(squad), Is.False, "Stays off while nothing changes.");
    }

    [Test]
    public void ARequestDuringTheCooldownIsAppliedWhenItEnds()
    {
        var squad = CreateLocalSquad(out _);
        em.SetComponentData(squad, new SquadStateComponent { formationChangeReadyAt = 2.0 });
        // Hold: FollowHero skips the cooldown on purpose.
        em.SetComponentData(squad, new SquadPlayerOrderIntentComponent
        {
            orderType = SquadOrderType.HoldPosition, desiredFormation = FormationType.Column,
        });
        Frame(1.0);
        Assert.That(Current(squad), Is.EqualTo(FormationType.Line), "Still cooling down.");
        Frame(2.0);
        Assert.That(Current(squad), Is.EqualTo(FormationType.Column), "Applied once the cooldown ends.");
        Assert.That(em.GetComponentData<SquadStateComponent>(squad).formationChangeReadyAt, Is.GreaterThan(2.0));
    }

    [Test]
    public void ADeathTurnsTheTagOn()
    {
        var squad = CreateLocalSquad(out var unit);
        em.AddComponentData(unit, new UnitOwnerComponent { squad = squad });
        em.AddComponent<UnitVisualReference>(unit);
        em.AddComponent<IsDeadComponent>(unit);
        world.GetOrCreateSystemManaged<UnitDeathSystem>().Update();
        Assert.That(em.GetBuffer<SquadUnitElement>(squad).Length, Is.Zero);
        Assert.That(em.IsComponentEnabled<FormationChangePending>(squad), Is.True);
    }
}
