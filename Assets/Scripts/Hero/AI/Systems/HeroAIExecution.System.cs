using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.AI;

// HasComponent<NavMeshAgent> must also be safe before any visual has been spawned.
[assembly: RegisterUnityEngineComponentType(typeof(NavMeshAgent))]

/// <summary>
/// Execution layer for the Remote Hero AI pipeline.
/// Reads <see cref="HeroAIDecision"/> (written by one behavior system) and translates it
/// into low-level commands that the existing game systems already understand:
///   - <see cref="HeroMoveIntent"/>     → consumed by visual movement and animation
///   - <see cref="NavMeshAgent"/>       → handles terrain-aware pathfinding
///   - <see cref="SquadAIOrderIntentComponent"/> → arbitrated by OrderResolutionSystem
///
/// Attack intent is NOT handled here — it is read directly by HeroAttackSystem's AI loop.
///
/// Pipeline: [Behavior systems] → THIS → HeroMovementSystem (local-only, unchanged)
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(HeroAIRusherSystem))]
[UpdateAfter(typeof(HeroAIBalancedSystem))]
[UpdateBefore(typeof(OrderResolutionSystem))]
public partial class HeroAIExecutionSystem : SystemBase
{
    private const float MinVelocitySqForIntent = 0.01f;    // below this desiredVelocity = not moving

    private ComponentLookup<HeroSquadReference>  _squadRefLookup;
    private ComponentLookup<SquadAIOrderIntentComponent> _squadIntentLookup;

    protected override void OnCreate()
    {
        _squadRefLookup   = GetComponentLookup<HeroSquadReference>(true);
        _squadIntentLookup = GetComponentLookup<SquadAIOrderIntentComponent>(false);
    }

    protected override void OnUpdate()
    {
        _squadRefLookup.Update(this);
        _squadIntentLookup.Update(this);
        bool hasConfig = SystemAPI.HasSingleton<SquadSpawnConfigComponent>();
        var config = hasConfig ? SystemAPI.GetSingleton<SquadSpawnConfigComponent>() : default;
        float sampleRadius = hasConfig ? math.max(0.01f, config.navMeshDestinationSampleRadius) : 2f;
        float retryDistance = hasConfig ? math.max(0.01f, config.navMeshFailureRetryDistance) : 1f;
        float arrivalDistance = hasConfig ? math.max(0f, config.heroAIArrivalDistance) : 1.5f;

        foreach (var (decision, transform, stats, life, entity) in
                 SystemAPI.Query<RefRW<HeroAIDecision>,
                                 RefRO<LocalTransform>,
                                 RefRO<HeroStatsComponent>,
                                 RefRO<HeroLifeComponent>>()
                          .WithAll<HeroAITag>()
                          .WithEntityAccess())
        {
            var dec = decision.ValueRO;

            // ── Movement via NavMeshAgent ─────────────────────────────────────────
            float3 moveDir = float3.zero;
            float  speed   = stats.ValueRO.baseSpeed;
            bool   shouldMove = life.ValueRO.isAlive && dec.action != AIActionType.Idle;

            // NavMeshAgent is added by HeroVisualInstantiationSystem when the visual prefab is ready.
            // Guard prevents ArgumentException during the frames between ECS spawn and visual instantiation.
            NavMeshAgent agent = null;
            if (EntityManager.HasComponent<NavMeshAgent>(entity))
                agent = EntityManager.GetComponentObject<NavMeshAgent>(entity);

            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                var navigation = EntityManager.HasComponent<NavAgentComponent>(entity)
                    ? EntityManager.GetComponentData<NavAgentComponent>(entity)
                    : default;
                if (shouldMove)
                {
                    float3 selfPos = transform.ValueRO.Position;
                    float3 requested = dec.targetPosition;
                    bool sameFailedCommand = navigation.commandFailed
                        && math.distancesq(navigation.lastFailedCommand, requested)
                            < math.square(retryDistance);

                    if (!sameFailedCommand
                        && UnitNavMeshSystem.TrySampleDestination(agent, requested,
                            sampleRadius, out float3 resolved))
                    {
                        bool previousPathFailed = navigation.hasIssuedCommand
                            && !navigation.lastCommandWasFormation
                            && !agent.pathPending
                            && math.distancesq(navigation.lastCommandDestination, resolved) <= 0.0001f
                            && agent.pathStatus != NavMeshPathStatus.PathComplete;
                        if (previousPathFailed)
                        {
                            RejectCommand(agent, requested, ref navigation);
                        }
                        else
                        {
                            navigation.commandFailed = false;
                            float distSq = math.distancesq(selfPos, resolved);
                            bool arrived = distSq <= math.square(arrivalDistance);

                            if (!arrived)
                            {
                                float agentSpeed = dec.shouldSprint
                                    ? stats.ValueRO.baseSpeed * stats.ValueRO.sprintMultiplier
                                    : stats.ValueRO.baseSpeed;
                                agent.speed = agentSpeed;
                                agent.isStopped = false;
                                if (agent.SetDestination(resolved))
                                {
                                    navigation.lastCommandDestination = resolved;
                                    navigation.hasIssuedCommand = true;
                                    navigation.lastCommandWasFormation = false;
                                }
                                else
                                {
                                    RejectCommand(agent, requested, ref navigation);
                                }

                                // Derive world-space direction from NavMesh desired velocity
                                // so visual synchronization can drive locomotion animation
                                UnityEngine.Vector3 vel = agent.desiredVelocity;
                                if (vel.sqrMagnitude > MinVelocitySqForIntent)
                                {
                                    moveDir = math.normalize(new float3(vel.x, vel.y, vel.z));
                                    speed = agentSpeed;
                                }
                            }
                            else
                            {
                                StopAgent(agent, ref navigation);
                            }
                        }
                    }
                    else
                    {
                        if (!sameFailedCommand)
                        {
                            RejectCommand(agent, requested, ref navigation);
                        }
                        else
                            StopAgent(agent, ref navigation, false);
                    }
                }
                else
                {
                    StopAgent(agent, ref navigation);
                }

                if (EntityManager.HasComponent<NavAgentComponent>(entity))
                    EntityManager.SetComponentData(entity, navigation);
            }

            // Publish movement intent for visual movement and animation consumers.
            if (SystemAPI.HasComponent<HeroMoveIntent>(entity))
                SystemAPI.SetComponent(entity, new HeroMoveIntent { Direction = moveDir, Speed = speed });

            // ── Squad Orders ──────────────────────────────────────────────────────
            if (life.ValueRO.isAlive && dec.hasNewSquadOrder && _squadRefLookup.HasComponent(entity))
            {
                Entity squadEntity = _squadRefLookup[entity].squad;
                if (SystemAPI.Exists(squadEntity) && _squadIntentLookup.HasComponent(squadEntity))
                {
                    // Publish even during combat: arbitration must not discard the request.
                    var intent = _squadIntentLookup[squadEntity];
                    intent.suggestedOrder = dec.squadOrder;
                    intent.holdPosition = dec.squadOrderPosition;
                    intent.targetEntity = Entity.Null;
                    _squadIntentLookup[squadEntity] = intent;
                }
            }

            // Clear the one-shot squad order flag
            decision.ValueRW.hasNewSquadOrder = false;
        }
    }

    private static void RejectCommand(NavMeshAgent agent, float3 requested,
        ref NavAgentComponent navigation)
    {
        navigation.lastFailedCommand = requested;
        navigation.commandFailed = true;
        StopAgent(agent, ref navigation);
    }

    private static void StopAgent(NavMeshAgent agent, ref NavAgentComponent navigation,
        bool resetPath = true)
    {
        if (resetPath && agent.isOnNavMesh) agent.ResetPath();
        agent.isStopped = true;
        navigation.hasIssuedCommand = false;
    }
}
