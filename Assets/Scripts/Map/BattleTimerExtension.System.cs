using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Listens for <see cref="ZoneCapturedEvent"/> entities and extends the battle timer
/// by 5 minutes per capture (capped at 30 min total by BattleSceneController).
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(CaptureZoneTriggerSystem))]
public partial class BattleTimerExtensionSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (evt, entity) in SystemAPI.Query<RefRO<ZoneCapturedEvent>>().WithEntityAccess())
        {
            var controller = Object.FindAnyObjectByType<BattleSceneController>();
            if (controller != null)
                controller.ExtendTimer(300); // +5 minutos

            ecb.DestroyEntity(entity);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
