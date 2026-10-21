using Unity.Entities;

/// <summary>
/// Counts the battle clock down while the match is <see cref="MatchState.InBattle"/> (L7-1). When it reaches zero the
/// defenders win, as before, but decided in ECS rather than by the HUD's countdown.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BattleBootstrapSystem))]
[UpdateBefore(typeof(MatchControllerSystem))]
public partial class BattleClockSystem : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<BattleClockComponent>();
        RequireForUpdate<MatchStateComponent>();
    }

    protected override void OnUpdate()
    {
        // More than one match state can exist briefly (subscene + system); act only on a single one.
        if (!SystemAPI.TryGetSingletonEntity<MatchStateComponent>(out var matchEntity)) return;
        if (!SystemAPI.TryGetSingletonEntity<BattleClockComponent>(out var clockEntity)) return;
        var match = SystemAPI.GetComponentRW<MatchStateComponent>(matchEntity);
        if (match.ValueRO.currentState != MatchState.InBattle) return;

        var clock = SystemAPI.GetComponentRW<BattleClockComponent>(clockEntity);
        if (clock.ValueRO.expired) return;
        clock.ValueRW.remainingSeconds = BattleClock.Tick(clock.ValueRO.remainingSeconds, SystemAPI.Time.DeltaTime);
        if (clock.ValueRO.remainingSeconds > 0f) return;

        clock.ValueRW.expired = true;
        if (!match.ValueRO.victoryConditionMet)
        {
            match.ValueRW.victoryConditionMet = true;
            match.ValueRW.winnerTeam = 2; // Defenders win on timer expiry.
        }
    }
}
