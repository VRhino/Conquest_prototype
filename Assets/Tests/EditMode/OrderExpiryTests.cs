using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>Order deadlines as end times carried by the resolved order (Aprendizajes_Referencia.md L3-05).</summary>
public class OrderExpiryTests
{
    World world;
    EntityManager em;

    [SetUp]
    public void SetUp()
    {
        world = new World("Order expiry");
        em = world.EntityManager;
        em.SetComponentData(em.CreateEntity(typeof(MatchStateComponent)),
            new MatchStateComponent { currentState = MatchState.InBattle });
    }

    [TearDown]
    public void TearDown() => world.Dispose();

    static SquadControlCommand Order(SquadOrderType order) => new SquadControlCommand { orderIssued = true, order = order };

    [Test]
    public void InsistingThreeTimesInTheWindowSetsAnOverrideEnd()
    {
        var input = new SquadInputComponent();
        var intent = new SquadPlayerOrderIntentComponent();
        SquadPlayerCommandResolver.TryApply(Order(SquadOrderType.Attack), default, ref input, ref intent, 10.0);
        SquadPlayerCommandResolver.TryApply(Order(SquadOrderType.Attack), default, ref input, ref intent, 10.3);
        Assert.That(intent.overrideEndsAt, Is.EqualTo(0.0), "Two presses are not insisting.");
        SquadPlayerCommandResolver.TryApply(Order(SquadOrderType.Attack), default, ref input, ref intent, 10.6);
        Assert.That(intent.overrideEndsAt, Is.GreaterThan(10.6), "The third press inside the window starts the override.");

        var late = new SquadPlayerOrderIntentComponent();
        SquadPlayerCommandResolver.TryApply(Order(SquadOrderType.Attack), default, ref input, ref late, 0.0);
        SquadPlayerCommandResolver.TryApply(Order(SquadOrderType.Attack), default, ref input, ref late, 5.0);
        SquadPlayerCommandResolver.TryApply(Order(SquadOrderType.Attack), default, ref input, ref late, 5.1);
        Assert.That(late.overrideEndsAt, Is.EqualTo(0.0), "A press after the window closed starts a new count.");
    }

    [Test]
    public void TheResolvedOrderCarriesTheOverrideEndAndCombatWinsOnceItLapses()
    {
        var hero = em.CreateEntity(typeof(LocalTransform), typeof(IsLocalPlayer));
        var squad = em.CreateEntity(typeof(SquadOwnerComponent), typeof(SquadInputComponent),
            typeof(SquadPlayerOrderIntentComponent), typeof(SquadAIOrderIntentComponent),
            typeof(SquadCombatReactionIntentComponent), typeof(SquadResolvedOrderComponent),
            typeof(SquadStateComponent), typeof(FormationComponent));
        em.SetComponentData(squad, new SquadOwnerComponent { hero = hero });
        var enemy = em.CreateEntity();
        em.SetComponentData(squad, new SquadCombatReactionIntentComponent { reactToEnemy = true, reactTarget = enemy });
        em.SetComponentData(squad, new SquadPlayerOrderIntentComponent
        {
            orderType = SquadOrderType.FollowHero, overrideEndsAt = 14.0,
        });
        var resolution = world.GetOrCreateSystemManaged<OrderResolutionSystem>();

        world.SetTime(new TimeData(10.0, 0.02f));
        resolution.Update();
        var resolved = em.GetComponentData<SquadResolvedOrderComponent>(squad);
        Assert.That(resolved.order, Is.EqualTo(SquadOrderType.FollowHero), "The insisted order beats the combat reaction.");
        Assert.That(resolved.expiresAt, Is.EqualTo(14.0), "...and says when it stops doing so.");
        Assert.That(resolved.OverridesCombat(10.0), Is.True);

        world.SetTime(new TimeData(14.0, 0.02f));
        resolution.Update();
        resolved = em.GetComponentData<SquadResolvedOrderComponent>(squad);
        Assert.That(resolved.order, Is.EqualTo(SquadOrderType.Attack), "Lapsed: the combat reaction wins again.");
        Assert.That(resolved.expiresAt, Is.EqualTo(0.0));
        Assert.That(resolved.hasNewOrder, Is.True);
    }
}
