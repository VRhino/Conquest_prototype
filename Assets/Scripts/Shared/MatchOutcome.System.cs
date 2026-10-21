using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Single resolver of the match result (Aprendizajes_Referencia.md L7-2): the only writer of
/// <see cref="MatchStateComponent.winnerTeam"/> and <see cref="MatchStateComponent.victoryConditionMet"/>. Consumes
/// <see cref="FinalZoneCapturedEvent"/> and <see cref="BattleTimeExpiredEvent"/> in a fixed order: a final capture wins
/// over the clock running out in the same frame, and a result already decided is never overwritten.
/// <see cref="MatchControllerSystem"/> then moves the match to <see cref="MatchState.EndMatch"/>.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(CaptureZoneTriggerSystem))]
[UpdateAfter(typeof(BattleClockSystem))]
[UpdateBefore(typeof(MatchControllerSystem))]
public partial class MatchOutcomeSystem : SystemBase
{
    protected override void OnUpdate()
    {
        bool captured = false, expired = false;
        int capturingTeam = MatchWinner.None;
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (evt, entity) in SystemAPI.Query<RefRO<FinalZoneCapturedEvent>>().WithEntityAccess())
        {
            if (!captured) capturingTeam = evt.ValueRO.capturingTeam;
            captured = true;
            ecb.DestroyEntity(entity);
        }
        foreach (var (_, entity) in SystemAPI.Query<RefRO<BattleTimeExpiredEvent>>().WithEntityAccess())
        {
            expired = true;
            ecb.DestroyEntity(entity);
        }
        ecb.Playback(EntityManager);
        ecb.Dispose();
        if (!captured && !expired) return;
        if (!SystemAPI.TryGetSingletonEntity<MatchStateComponent>(out var matchEntity)) return;

        var match = SystemAPI.GetComponentRW<MatchStateComponent>(matchEntity);
        if (match.ValueRO.victoryConditionMet) return;
        match.ValueRW.victoryConditionMet = true;
        match.ValueRW.winnerTeam = Resolve(captured, capturingTeam, expired);

        if (captured && SystemAPI.TryGetSingletonEntity<GameStateComponent>(out var stateEntity))
            SystemAPI.GetComponentRW<GameStateComponent>(stateEntity).ValueRW.currentPhase = GamePhase.PostPartida;
    }

    /// <summary>Winner of this frame's events: the capturing team, else the defenders if time ran out.</summary>
    public static int Resolve(bool finalCaptured, int capturingTeam, bool timeExpired)
    {
        if (finalCaptured) return capturingTeam;
        return timeExpired ? MatchWinner.Defenders : MatchWinner.None;
    }
}
