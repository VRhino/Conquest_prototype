using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Listens for <see cref="ZoneCapturedEvent"/> entities and extends the battle clock by the map's capture extension,
/// never above its maximum (<see cref="BattleClock.Extend"/>). The clock lives in ECS (L7-1); no GameObject lookup.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(CaptureZoneTriggerSystem))]
public partial class BattleTimerExtensionSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        bool hasClock = SystemAPI.TryGetSingletonEntity<BattleClockComponent>(out var clockEntity);

        foreach (var (evt, entity) in SystemAPI.Query<RefRO<ZoneCapturedEvent>>().WithEntityAccess())
        {
            if (hasClock)
            {
                var clock = SystemAPI.GetComponentRW<BattleClockComponent>(clockEntity);
                if (!clock.ValueRO.expired)
                    clock.ValueRW.remainingSeconds = BattleClock.Extend(clock.ValueRO.remainingSeconds,
                        clock.ValueRO.captureExtensionSeconds, clock.ValueRO.maxSeconds);
            }
            ecb.DestroyEntity(entity);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
