using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.AI;

/// <summary>
/// Moves the leader of a squad towards <see cref="SquadNavigationComponent.targetPosition"/>
/// using a NavMeshAgent when available.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class SquadNavigationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var transformLookup = GetComponentLookup<LocalTransform>(true);
        var navAgentLookup = GetComponentLookup<NavAgentComponent>();

        foreach (var (nav, state, units, entity) in SystemAPI
                     .Query<RefRW<SquadNavigationComponent>,
                            RefRO<SquadStateComponent>,
                            DynamicBuffer<SquadUnitElement>>()
                     .WithEntityAccess())
        {
            if (!nav.ValueRO.isNavigating || units.Length == 0)
                continue;

            Entity leader = units[0].Value;
            if (!SystemAPI.Exists(leader) || !transformLookup.HasComponent(leader))
                continue;

            float3 leaderPos = transformLookup[leader].Position;
            float distSq = math.distancesq(leaderPos, nav.ValueRO.targetPosition);
            if (distSq <= nav.ValueRO.arrivalThreshold * nav.ValueRO.arrivalThreshold)
            {
                nav.ValueRW.isNavigating = false;
                continue;
            }

            if (navAgentLookup.HasComponent(leader))
            {
                var agent = SystemAPI.ManagedAPI.GetComponent<NavMeshAgent>(leader);
                if (agent != null && agent.enabled)
                    agent.SetDestination(nav.ValueRO.targetPosition);
            }
        }
    }
}
