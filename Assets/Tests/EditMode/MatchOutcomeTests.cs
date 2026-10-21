using NUnit.Framework;
using Unity.Entities;

/// <summary>Single resolver of the match result (L7-2).</summary>
public class MatchOutcomeTests
{
    [Test]
    public void FinalCaptureBeatsTheClockInTheSameFrame()
    {
        Assert.That(MatchOutcomeSystem.Resolve(true, MatchWinner.Attackers, true), Is.EqualTo(MatchWinner.Attackers));
        Assert.That(MatchOutcomeSystem.Resolve(false, MatchWinner.None, true), Is.EqualTo(MatchWinner.Defenders));
        Assert.That(MatchOutcomeSystem.Resolve(false, MatchWinner.None, false), Is.EqualTo(MatchWinner.None));
    }

    [Test]
    public void ResolverWritesTheResultOnceAndConsumesTheEvents()
    {
        using var world = new World("Match outcome");
        var em = world.EntityManager;
        var match = em.CreateEntity(typeof(MatchStateComponent));
        em.SetComponentData(match, new MatchStateComponent { currentState = MatchState.InBattle });
        var system = world.GetOrCreateSystemManaged<MatchOutcomeSystem>();

        em.SetComponentData(em.CreateEntity(typeof(FinalZoneCapturedEvent)),
            new FinalZoneCapturedEvent { capturingTeam = MatchWinner.Attackers });
        em.CreateEntity(typeof(BattleTimeExpiredEvent));
        system.Update();
        var state = em.GetComponentData<MatchStateComponent>(match);
        Assert.That(state.victoryConditionMet, Is.True);
        Assert.That(state.winnerTeam, Is.EqualTo(MatchWinner.Attackers));

        // A later event cannot overwrite a decided match.
        em.CreateEntity(typeof(BattleTimeExpiredEvent));
        system.Update();
        Assert.That(em.GetComponentData<MatchStateComponent>(match).winnerTeam, Is.EqualTo(MatchWinner.Attackers));
        using var left = em.CreateEntityQuery(typeof(BattleTimeExpiredEvent));
        Assert.That(left.CalculateEntityCount(), Is.EqualTo(0));
    }
}
