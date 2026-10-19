using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.AI;
using ConquestTactics.Animation;

/// <summary>
/// Syncs NavMesh agent position (GO authoritative) → ECS LocalTransform.Position
/// for entities with NavAgentComponent.syncPositionFromNavMesh == true, and
/// snapshots confirmed motion for UnitAnimationSystem.
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
        var animationLookup = GetComponentLookup<UnitAnimationMovementComponent>();
        foreach (var (navAgent, transform, entity) in SystemAPI
                     .Query<RefRO<NavAgentComponent>, RefRW<LocalTransform>>()
                     .WithEntityAccess())
        {
            bool hasAnimationData = animationLookup.HasComponent(entity);
            if (!navAgent.ValueRO.syncPositionFromNavMesh && !hasAnimationData)
                continue;

            var agent = EntityManager.HasComponent<NavMeshAgent>(entity)
                ? SystemAPI.ManagedAPI.GetComponent<NavMeshAgent>(entity)
                : null;
            bool hasAgent = agent != null;
            bool hasValidAgent = hasAgent && agent.enabled && agent.isOnNavMesh;
            if (animationLookup.HasComponent(entity))
            {
                var animation = animationLookup[entity];
                animation.HasNavMeshAgentSnapshot = hasAgent;
                animation.HasValidNavMeshSnapshot = hasValidAgent;
                if (hasValidAgent)
                {
                    var velocity = agent.velocity;
                    animation.NavMeshVelocity = new float3(velocity.x, 0f, velocity.z);
                }
                else
                {
                    animation.NavMeshVelocity = float3.zero;
                }
                if (hasValidAgent)
                    animation.RequestedSpeed = agent.speed;
                animationLookup[entity] = animation;
            }

            if (!navAgent.ValueRO.syncPositionFromNavMesh || !hasValidAgent)
                continue;

            UnityEngine.Vector3 p = agent.transform.position;
            transform.ValueRW.Position = new float3(p.x, p.y, p.z);
        }
    }
}
