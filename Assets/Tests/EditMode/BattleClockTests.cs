using NUnit.Framework;
using Unity.Entities;

/// <summary>Battle clock in ECS (L7-1).</summary>
public class BattleClockTests
{
    [Test]
    public void StartingTimeComesFromTheBattleThenTheMapThenTheDefault()
    {
        Assert.That(BattleClock.InitialSeconds(600, 1800), Is.EqualTo(600));
        Assert.That(BattleClock.InitialSeconds(0, 1200), Is.EqualTo(1200));
        Assert.That(BattleClock.InitialSeconds(0, 0), Is.EqualTo(BattleClock.DefaultBattleSeconds));
    }

    [Test]
    public void CaptureExtendsUpToTheCapAndNeverLowersIt()
    {
        Assert.That(BattleClock.Extend(600f, 300f, 1800f), Is.EqualTo(900f));
        Assert.That(BattleClock.Extend(1700f, 300f, 1800f), Is.EqualTo(1800f));
        Assert.That(BattleClock.Extend(1900f, 300f, 1800f), Is.EqualTo(1900f));
        Assert.That(BattleClock.Format(65.2f), Is.EqualTo("01:06"));
        Assert.That(BattleClock.Format(0f), Is.EqualTo("00:00"));
    }

    [Test]
    public void SystemCountsDownOnlyInBattleAndExpiresOnce()
    {
        using var world = new World("Battle clock");
        var em = world.EntityManager;
        var match = em.CreateEntity(typeof(MatchStateComponent));
        em.SetComponentData(match, new MatchStateComponent { currentState = MatchState.PreparationPhase });
        var clock = em.CreateEntity(typeof(BattleClockComponent));
        em.SetComponentData(clock, new BattleClockComponent { remainingSeconds = 1f, maxSeconds = 1800f });
        var system = world.GetOrCreateSystemManaged<BattleClockSystem>();

        world.SetTime(new Unity.Core.TimeData(0, 0.5f));
        system.Update();
        Assert.That(em.GetComponentData<BattleClockComponent>(clock).remainingSeconds, Is.EqualTo(1f),
            "The clock must not run outside the battle.");

        em.SetComponentData(match, new MatchStateComponent { currentState = MatchState.InBattle });
        system.Update();
        Assert.That(em.GetComponentData<BattleClockComponent>(clock).remainingSeconds, Is.EqualTo(0.5f).Within(1e-5f));
        Assert.That(em.GetComponentData<MatchStateComponent>(match).victoryConditionMet, Is.False);

        system.Update();
        Assert.That(em.GetComponentData<BattleClockComponent>(clock).expired, Is.True);
        using var events = em.CreateEntityQuery(typeof(BattleTimeExpiredEvent));
        Assert.That(events.CalculateEntityCount(), Is.EqualTo(1));
        system.Update(); // Expired: no second event.
        Assert.That(events.CalculateEntityCount(), Is.EqualTo(1));

        // The single resolver turns it into the defenders' victory.
        world.GetOrCreateSystemManaged<MatchOutcomeSystem>().Update();
        var state = em.GetComponentData<MatchStateComponent>(match);
        Assert.That(state.victoryConditionMet, Is.True);
        Assert.That(state.winnerTeam, Is.EqualTo(MatchWinner.Defenders));
        Assert.That(events.CalculateEntityCount(), Is.EqualTo(0), "Events are consumed.");
    }
}
