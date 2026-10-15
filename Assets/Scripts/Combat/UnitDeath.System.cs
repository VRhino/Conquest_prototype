using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Consumes IsDeadComponent on units: destroys their visual GO if present, removes the unit
/// from its squad's SquadUnitElement buffer, and destroys the ECS entity.
/// Runs after damage playback and before enemy detection and targeting.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(DamageCalculationSystem))]
[UpdateBefore(typeof(EnemyDetectionSystem))]
[UpdateBefore(typeof(UnitTargetingSystem))]
public partial class UnitDeathSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (_, entity) in
                 SystemAPI.Query<RefRO<UnitVisualReference>>()
                          .WithAll<IsDeadComponent>()
                          .WithEntityAccess())
        {
            // Gameplay cleanup must not depend on successful visual instantiation.
            Entity squad = EntityManager.HasComponent<UnitOwnerComponent>(entity)
                ? EntityManager.GetComponentData<UnitOwnerComponent>(entity).squad
                : Entity.Null;
            if (EntityManager.HasComponent<UnitVisualInstance>(entity))
            {
                var visualInstance = EntityManager.GetComponentData<UnitVisualInstance>(entity);
                // The unit's managed agent points directly at its visual in the
                // normal spawn path. Older visuals without an agent keep the fallback.
                var go = EntityManager.HasComponent<NavMeshAgent>(entity)
                    ? EntityManager.GetComponentObject<NavMeshAgent>(entity)?.gameObject
                    : null;
                if (go == null)
                    go = FindGameObjectByInstanceId(visualInstance.visualInstanceId);
                if (go != null) Object.Destroy(go);
                if (squad == Entity.Null || !EntityManager.Exists(squad))
                    squad = visualInstance.parentSquad;
            }

            if (squad == Entity.Null || !EntityManager.HasBuffer<SquadUnitElement>(squad))
                squad = FindParentSquad(entity);

            // Remove unit from squad buffer.
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

            // Destroy ECS entity.
            ecb.DestroyEntity(entity);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }

    private Entity FindParentSquad(Entity unit)
    {
        foreach (var (units, squad) in SystemAPI.Query<DynamicBuffer<SquadUnitElement>>().WithEntityAccess())
            for (int i = 0; i < units.Length; i++)
                if (units[i].Value == unit) return squad;
        return Entity.Null;
    }

    private static GameObject FindGameObjectByInstanceId(EntityId instanceId)
    {
        // Visual lookup is limited to the rare unit death event.
        var all = Object.FindObjectsOfType<GameObject>();
        return System.Array.Find(all, o => o.GetEntityId() == instanceId);
    }
}
