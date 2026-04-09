using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.AI;

/// <summary>
/// Execution layer for the Remote Hero AI pipeline.
/// Reads <see cref="HeroAIDecision"/> (written by one behavior system) and translates it
/// into low-level commands that the existing game systems already understand:
///   - <see cref="HeroMoveIntent"/>     → picked up by HeroStateSystem for animation
///   - <see cref="NavMeshAgent"/>       → handles terrain-aware pathfinding
///   - <see cref="SquadInputComponent"/> → picked up by SquadOrderSystem (no changes needed there)
///
/// Attack intent is NOT handled here — it is read directly by HeroAttackSystem's AI loop.
///
/// Pipeline: [Behavior systems] → THIS → HeroMovementSystem (local-only, unchanged)
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(HeroAIRusherSystem))]
[UpdateAfter(typeof(HeroAIBalancedSystem))]
public partial class HeroAIExecutionSystem : SystemBase
{
    private const float ArrivalDistanceSq  = 1.5f * 1.5f;  // stop moving when this close to target
    private const float MinVelocitySqForIntent = 0.01f;    // below this desiredVelocity = not moving

    private ComponentLookup<HeroSquadReference>  _squadRefLookup;
    private ComponentLookup<SquadInputComponent> _squadInputLookup;
    private ComponentLookup<SquadStateComponent> _squadStateLookup;

    protected override void OnCreate()
    {
        _squadRefLookup   = GetComponentLookup<HeroSquadReference>(true);
        _squadInputLookup = GetComponentLookup<SquadInputComponent>(false);
        _squadStateLookup = GetComponentLookup<SquadStateComponent>(true);
    }

    protected override void OnUpdate()
    {
        _squadRefLookup.Update(this);
        _squadInputLookup.Update(this);
        _squadStateLookup.Update(this);

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

            if (agent != null)
            {
                if (shouldMove)
                {
                    float3 selfPos = transform.ValueRO.Position;
                    float  distSq  = math.distancesq(selfPos, dec.targetPosition);
                    bool   arrived = distSq <= ArrivalDistanceSq;

                    if (!arrived)
                    {
                        float agentSpeed = dec.shouldSprint
                            ? stats.ValueRO.baseSpeed * stats.ValueRO.sprintMultiplier
                            : stats.ValueRO.baseSpeed;
                        agent.speed     = agentSpeed;
                        agent.isStopped = false;
                        agent.SetDestination(new UnityEngine.Vector3(
                            dec.targetPosition.x, dec.targetPosition.y, dec.targetPosition.z));

                        // Derive world-space direction from NavMesh desired velocity
                        // so HeroStateSystem can detect movement → play walk/run animation
                        UnityEngine.Vector3 vel = agent.desiredVelocity;
                        if (vel.sqrMagnitude > MinVelocitySqForIntent)
                        {
                            moveDir = math.normalize(new float3(vel.x, vel.y, vel.z));
                            speed   = agentSpeed;
                        }
                    }
                    else
                    {
                        agent.isStopped = true;
                    }
                }
                else
                {
                    agent.isStopped = true;
                }
            }

            // Write HeroMoveIntent so HeroStateSystem picks up movement state for animations
            if (SystemAPI.HasComponent<HeroMoveIntent>(entity))
                SystemAPI.SetComponent(entity, new HeroMoveIntent { Direction = moveDir, Speed = speed });

            // ── Squad Orders ──────────────────────────────────────────────────────
            if (life.ValueRO.isAlive && dec.hasNewSquadOrder && _squadRefLookup.HasComponent(entity))
            {
                Entity squadEntity = _squadRefLookup[entity].squad;
                if (SystemAPI.Exists(squadEntity) && _squadInputLookup.HasComponent(squadEntity))
                {
                    // BUG-006: block movement orders while squad is in active combat.
                    // Attack orders are always allowed; movement orders would cause units to
                    // physically leave detection range and break the combat engagement.
                    bool isMovementOrder = dec.squadOrder == SquadOrderType.FollowHero
                                       || dec.squadOrder == SquadOrderType.HoldPosition;
                    bool squadInCombat   = _squadStateLookup.HasComponent(squadEntity)
                                       && _squadStateLookup[squadEntity].currentState == SquadFSMState.InCombat;

                    if (!isMovementOrder || !squadInCombat)
                    {
                        var squadInput          = _squadInputLookup[squadEntity];
                        squadInput.orderType    = dec.squadOrder;
                        squadInput.holdPosition = dec.squadOrderPosition;
                        squadInput.hasNewOrder  = true;
                        _squadInputLookup[squadEntity] = squadInput;
                    }
                }
            }

            // Clear the one-shot squad order flag
            decision.ValueRW.hasNewSquadOrder = false;
        }
    }
}
