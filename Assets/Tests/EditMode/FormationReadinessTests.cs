using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// F12 (Docs/Por_Abordar/Formaciones_y_Posturas.md): a formation is ready when at most ceil((1−q)·N) soldiers have not
/// arrived, isolated ones left out, and an AI hero waits for it before ordering its squad to charge.
/// </summary>
public class FormationReadinessTests
{
    static readonly FormationReadinessTuning Tuning = FormationReadinessTuning.Default;

    [TestCase(2, 10, 0.8f, true)]
    [TestCase(3, 10, 0.8f, false)]
    [TestCase(1, 4, 0.8f, true)]   // ceil(0.8) = 1
    [TestCase(2, 4, 0.8f, false)]
    [TestCase(1, 10, 1f, false)]   // Everyone has to be there.
    [TestCase(10, 10, 0f, true)]
    [TestCase(0, 0, 0.8f, true)]   // Nobody left to wait for.
    public void ReadyWithAtMostTheQuorumsShareMissing(int stragglers, int counted, float quorum, bool ready)
    {
        Assert.That(FormationLayout.IsFormationReady(stragglers, counted, quorum), Is.EqualTo(ready));
    }

    static HeroAIDecision Charge() => new()
    {
        action = AIActionType.AttackTarget,
        shouldAttack = true,
        squadOrder = SquadOrderType.Attack,
        hasNewSquadOrder = true,
    };

    static HeroAIBlackboard Waiting() => new()
    {
        hasSquad = true,
        squadFormationReady = false,
        squadIsInCombat = false,
        nearestEnemyHero = new Entity { Index = 1, Version = 1 },
        nearestEnemyDistanceSq = 15f * 15f,
        selfPosition = new float3(4f, 0f, 2f),
    };

    [Test]
    public void TheHeroHoldsTheChargeAndCallsTheSquadToIt()
    {
        var dec = Charge();
        var bb = Waiting();
        Assert.That(HeroAISquadWait.Hold(ref dec, bb, 10.0, Tuning), Is.True);
        Assert.That(dec.action, Is.EqualTo(AIActionType.Idle));
        Assert.That(dec.shouldAttack, Is.False);
        Assert.That(dec.squadOrder, Is.EqualTo(SquadOrderType.FollowHero));
        Assert.That(dec.hasNewSquadOrder, Is.True, "The squad is told to form on the hero.");
        Assert.That(dec.targetPosition, Is.EqualTo(bb.selfPosition));
    }

    [Test]
    public void ItChargesWhenReadyFightingWithTheEnemyOnItOrWithoutASquad()
    {
        var bb = Waiting();
        bb.squadFormationReady = true;
        var dec = Charge();
        Assert.That(HeroAISquadWait.Hold(ref dec, bb, 0.0, Tuning), Is.False, "Ready.");

        bb = Waiting();
        bb.squadIsInCombat = true;
        dec = Charge();
        Assert.That(HeroAISquadWait.Hold(ref dec, bb, 0.0, Tuning), Is.False, "Already fighting.");

        bb = Waiting();
        bb.nearestEnemyDistanceSq = math.square(Tuning.heroWaitEnemyDistance * 0.5f);
        dec = Charge();
        Assert.That(HeroAISquadWait.Hold(ref dec, bb, 0.0, Tuning), Is.False, "The enemy hero is on it.");

        bb = Waiting();
        bb.hasSquad = false;
        dec = Charge();
        Assert.That(HeroAISquadWait.Hold(ref dec, bb, 0.0, Tuning), Is.False, "No squad to wait for.");
        Assert.That(dec.squadOrder, Is.EqualTo(SquadOrderType.Attack));
        Assert.That(dec.action, Is.EqualTo(AIActionType.AttackTarget));
    }

    [Test]
    public void OneChargeWaitsAtMostTheMaximumAndTheNextGetsItsOwnWait()
    {
        var bb = Waiting();
        var dec = Charge();
        Assert.That(HeroAISquadWait.Hold(ref dec, bb, 5.0, Tuning), Is.True);
        dec = Charge();
        Assert.That(HeroAISquadWait.Hold(ref dec, bb, 5.0 + Tuning.heroMaxWait * 0.5, Tuning), Is.True);
        dec = Charge();
        Assert.That(HeroAISquadWait.Hold(ref dec, bb, 5.0 + Tuning.heroMaxWait, Tuning), Is.False, "Waited enough.");

        // Another decision ends the charge; the next one waits again.
        var move = new HeroAIDecision { action = AIActionType.CaptureZone, squadOrder = SquadOrderType.FollowHero, hasNewSquadOrder = true };
        Assert.That(HeroAISquadWait.Hold(ref move, bb, 20.0, Tuning), Is.False);
        Assert.That(move.action, Is.EqualTo(AIActionType.CaptureZone), "Only a charge is ever held.");
        dec = Charge();
        Assert.That(HeroAISquadWait.Hold(ref dec, bb, 21.0, Tuning), Is.True);
    }

    [Test]
    public void TheSquadAICountsStragglersAndLeavesTheIsolatedOut()
    {
        using var world = new World("Formation readiness");
        var em = world.EntityManager;
        em.CreateEntity(typeof(MatchStateComponent));

        var squad = em.CreateEntity();
        em.AddComponentData(squad, new SquadAIComponent());
        em.AddComponentData(squad, new SquadStateComponent());
        em.AddComponentData(squad, new SquadDataReference());
        em.AddComponentData(squad, new SquadDefinitionComponent { behaviorProfile = BehaviorProfile.Versatile });
        em.AddComponentData(squad, new SquadFormationAnchorComponent());
        em.AddBuffer<SquadUnitElement>(squad);

        // Nine soldiers that count (two away from their slot) and one isolated far away: 2 ≤ ceil(0.2·9) = 2.
        float far = Tuning.stragglerDistance * 2f, near = Tuning.stragglerDistance * 0.5f;
        AddUnit(em, squad, near, FormationSlotResolveStatus.Template, 7);
        AddUnit(em, squad, far, FormationSlotResolveStatus.Relocated, 2);
        AddUnit(em, squad, far * 5f, FormationSlotResolveStatus.Unresolved, 1);

        var system = world.GetOrCreateSystemManaged<SquadAISystem>();
        system.Update();
        var ai = em.GetComponentData<SquadAIComponent>(squad);
        Assert.That(ai.stragglers, Is.EqualTo(2));
        Assert.That(ai.isolated, Is.EqualTo(1));
        Assert.That(ai.formationReady, Is.True);
        Assert.That(ai.tacticalIntent, Is.EqualTo(TacticalIntent.Idle));

        AddUnit(em, squad, far, FormationSlotResolveStatus.Template, 1); // 3 of 10 now.
        system.Update();
        ai = em.GetComponentData<SquadAIComponent>(squad);
        Assert.That(ai.stragglers, Is.EqualTo(3));
        Assert.That(ai.formationReady, Is.False);
        Assert.That(ai.tacticalIntent, Is.EqualTo(TacticalIntent.Regrouping), "No enemy in sight: it regroups.");
    }

    static void AddUnit(EntityManager em, Entity squad, float offset, FormationSlotResolveStatus status, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var slot = new float3(i * 2f, 0f, 0f);
            var unit = em.CreateEntity();
            em.AddComponentData(unit, LocalTransform.FromPosition(slot + new float3(0f, 0.3f, offset)));
            em.AddComponentData(unit, new UnitResolvedSlotComponent { position = slot, status = status });
            em.GetBuffer<SquadUnitElement>(squad).Add(new SquadUnitElement { Value = unit });
        }
    }
}
