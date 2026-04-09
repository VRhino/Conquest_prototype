using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Consumes IsDeadComponent: destroys the unit's visual GO, removes the unit
/// from its squad's SquadUnitElement buffer, and destroys the ECS entity.
/// Runs the frame after DamageCalculationSystem sets IsDeadComponent.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(DamageCalculationSystem))]
public partial class UnitDeathSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (visualInstance, entity) in
                 SystemAPI.Query<RefRO<UnitVisualInstance>>()
                          .WithAll<IsDeadComponent>()
                          .WithEntityAccess())
        {
            // 1. Destroy visual GO
            var go = FindGameObjectByInstanceId(visualInstance.ValueRO.visualInstanceId);
            if (go != null) Object.Destroy(go);

            // 2. Remove unit from squad buffer
            Entity squad = visualInstance.ValueRO.parentSquad;
            if (squad != Entity.Null && EntityManager.HasBuffer<SquadUnitElement>(squad))
            {
                var buffer = EntityManager.GetBuffer<SquadUnitElement>(squad);
                for (int i = buffer.Length - 1; i >= 0; i--)
                {
                    if (buffer[i].Value == entity)
                    {
                        buffer.RemoveAt(i);
                        break;
                    }
                }
            }

            // 3. Destroy ECS entity
            ecb.DestroyEntity(entity);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }

    private static GameObject FindGameObjectByInstanceId(int instanceId)
    {
        // Same pattern as HeroVisualEquipmentSystem — acceptable cost for rare death events
        var all = Object.FindObjectsOfType<GameObject>();
        return System.Array.Find(all, o => o.GetInstanceID() == instanceId);
    }
}
