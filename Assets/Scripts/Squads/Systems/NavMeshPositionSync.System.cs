using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.AI;

/// <summary>
/// Syncs NavMesh agent position (GO authoritative) → ECS LocalTransform.Position
/// for all entities with NavAgentComponent.syncPositionFromNavMesh == true.
///
/// Replaces the GO→ECS position write that was previously in EntityVisualSync.
/// EntityVisualSync skips its position sync for these entities.
///
/// Runs after UnitNavMeshSystem (which issues SetDestination) so the agent
/// has already moved before we capture its position into ECS.
/// Runs before UnitRotationResolutionSystem so that position is up-to-date
/// when rotation is resolved.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitNavMeshSystem))]
[UpdateBefore(typeof(UnitRotationResolutionSystem))]
public partial class NavMeshPositionSyncSystem : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<MatchStateComponent>();
    }

    protected override void OnUpdate()
    {
        foreach (var (navAgent, transform, entity) in SystemAPI
                     .Query<RefRO<NavAgentComponent>, RefRW<LocalTransform>>()
                     .WithEntityAccess())
        {
            if (!navAgent.ValueRO.syncPositionFromNavMesh)
                continue;

            var agent = SystemAPI.ManagedAPI.GetComponent<NavMeshAgent>(entity);
            if (agent == null || !agent.isOnNavMesh)
                continue;

            UnityEngine.Vector3 p = agent.transform.position;
            transform.ValueRW.Position = new float3(p.x, p.y, p.z);
        }
    }
}
