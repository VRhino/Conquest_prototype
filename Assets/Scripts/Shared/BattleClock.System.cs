using Unity.Entities;

/// <summary>
/// Counts the battle clock down while the match is <see cref="MatchState.InBattle"/> (L7-1). When it reaches zero it
/// emits <see cref="BattleTimeExpiredEvent"/> once; <see cref="MatchOutcomeSystem"/> decides the result (L7-2).
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
        if (SystemAPI.GetComponent<MatchStateComponent>(matchEntity).currentState != MatchState.InBattle) return;

        var clock = SystemAPI.GetComponentRW<BattleClockComponent>(clockEntity);
        if (clock.ValueRO.expired) return;
        clock.ValueRW.remainingSeconds = BattleClock.Tick(clock.ValueRO.remainingSeconds, SystemAPI.Time.DeltaTime);
        if (clock.ValueRO.remainingSeconds > 0f) return;

        clock.ValueRW.expired = true;
        EntityManager.CreateEntity(typeof(BattleTimeExpiredEvent));
    }
}
