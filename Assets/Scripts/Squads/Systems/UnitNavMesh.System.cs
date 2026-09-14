using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.AI;

/// <summary>
/// Single authority for all NavMesh movement decisions per unit:
///   - SetDestination: formation slot OR stop-point near combat target
///   - updateRotation: NavMesh owns it while moving; disabled + manual when
///     in engagement range so the unit faces its target for the AABB check.
///
/// Runs BEFORE UnitFollowFormationSystem so that Formed-state orientation
/// (hero/hold direction) can override combat rotation as a higher-priority
/// last-write — which is correct: formed units maintain squad discipline.
///
/// Pipeline position:
///   UnitFormationStateSystem
///       ↓
///   [UnitNavMeshSystem]         ← this system (movement + combat rotation)
///       ↓
///   UnitFollowFormationSystem   (Formed-state orientation override, runs after)
///       ↓
///   UnitBodyblockSystem
///       ↓
///   UnitAttackSystem
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitFormationStateSystem))]
[UpdateAfter(typeof(UnitTargetingSystem))]
[UpdateBefore(typeof(UnitFollowFormationSystem))]
public partial class UnitNavMeshSystem : SystemBase
{
    // Stop at 75 % of attackRange — keeps unit inside the directional AABB.
    private const float StopDistanceFactor = 0.75f;

    // Within this distance the unit turns to face the target manually.
    private const float EngagementRange = 3.5f;

    // Unit → squad order map, rebuilt every frame.
    private NativeHashMap<Entity, SquadOrderType> _unitToOrder;

    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<MatchStateComponent>();
        _unitToOrder    = new NativeHashMap<Entity, SquadOrderType>(256, Allocator.Persistent);
    }

    protected override void OnDestroy()
    {
        if (_unitToOrder.IsCreated)    _unitToOrder.Dispose();
    }

    protected override void OnUpdate()
    {
        // ── Phase 0: build unit → squad order map ────────────
        _unitToOrder.Clear();

        foreach (var (state, units, squadEntity) in
            SystemAPI.Query<RefRO<SquadStateComponent>, DynamicBuffer<SquadUnitElement>>()
                     .WithEntityAccess())
        {
            SquadOrderType order    = state.ValueRO.currentOrder;
            for (int i = 0; i < units.Length; i++)
            {
                Entity u = units[i].Value;
                if (u != Entity.Null)
                {
                    _unitToOrder.TryAdd(u, order);
                }
            }
        }

        // Read leash distance once — avoids per-unit singleton lookup
        bool hasConfig = SystemAPI.HasSingleton<SquadSpawnConfigComponent>();
        var config = hasConfig ? SystemAPI.GetSingleton<SquadSpawnConfigComponent>() : default;
        float leashDistance = hasConfig ? config.unitLeashDistance : 6f;
        float sampleRadius = hasConfig ? math.max(0.01f, config.navMeshDestinationSampleRadius) : 2f;
        float retryDistance = hasConfig ? math.max(0.01f, config.navMeshFailureRetryDistance) : 1f;
        float arrivalThreshold = hasConfig ? math.max(0.01f, config.slotArrivalThreshold) : 0.2f;

        // ── Phase 1: movement + rotation decision per NavMesh unit ───────────
        foreach (var (targetPos, formState, navigationState, transform, entity) in
            SystemAPI.Query<RefRO<UnitTargetPositionComponent>,
                            RefRO<UnitFormationStateComponent>,
                            RefRW<NavAgentComponent>,
                            RefRW<LocalTransform>>()
                     .WithEntityAccess())
        {
            var agent = SystemAPI.ManagedAPI.GetComponent<NavMeshAgent>(entity);
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
                continue;

            agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;

            float3 unitPos = transform.ValueRO.Position;
            float3 requestedSlot = targetPos.ValueRO.position;
            var navigation = navigationState.ValueRO;
            DetectFailedFormationPath(agent, requestedSlot, unitPos, retryDistance,
                arrivalThreshold, ref navigation);
            float3 formationDestination = ResolveFormationDestination(agent, requestedSlot,
                unitPos, sampleRadius, retryDistance, ref navigation);
            float3 destination = formationDestination;
            bool usesFormationDestination = true;

            // Tactical order controls pursuit independently of combat state.
            _unitToOrder.TryGetValue(entity, out SquadOrderType squadOrder);

            // Read optional combat components
            bool   hasCombat    = SystemAPI.HasComponent<UnitCombatComponent>(entity)
                                && SystemAPI.HasComponent<UnitWeaponComponent>(entity);
            bool   isRanged     = SystemAPI.HasComponent<UnitRangedStatsComponent>(entity);
            Entity combatTarget = Entity.Null;
            float  attackRange  = 1.5f; // fallback

            if (hasCombat)
            {
                combatTarget = SystemAPI.GetComponent<UnitCombatComponent>(entity).target;
                attackRange  = SystemAPI.GetComponent<UnitWeaponComponent>(entity).attackRange;
                // Ranged units must stop at bow range, not melee weapon range
                if (isRanged)
                    attackRange = SystemAPI.GetComponent<UnitRangedStatsComponent>(entity).range;

                if (combatTarget != Entity.Null && !SystemAPI.Exists(combatTarget))
                    combatTarget = Entity.Null;

                // A combat state does not authorize pursuit outside the formation leash.
                if (combatTarget != Entity.Null
                    && SystemAPI.HasComponent<LocalTransform>(combatTarget))
                {
                    float3 slotPos    = targetPos.ValueRO.position;
                    float3 enemyPos3D = SystemAPI.GetComponent<LocalTransform>(combatTarget).Position;
                    if (squadOrder != SquadOrderType.HoldPosition
                        && !CanPursueTarget(squadOrder, slotPos, enemyPos3D, leashDistance))
                        combatTarget = Entity.Null; // out of leash — return to formation slot
                }
            }

            // HoldPosition: unidades van a su slot y se quedan; no persiguen targets.
            bool isHoldingPosition = squadOrder == SquadOrderType.HoldPosition;

            if (combatTarget != Entity.Null
                && SystemAPI.HasComponent<LocalTransform>(combatTarget))
            {
                float3 targetWorldPos = SystemAPI.GetComponent<LocalTransform>(combatTarget).Position;
                float2 unitXZ   = new float2(unitPos.x,        unitPos.z);
                float2 targetXZ = new float2(targetWorldPos.x, targetWorldPos.z);
                float  dist     = math.distance(unitXZ, targetXZ);
                
                // Ranged units stop immediately when in range; melee units push closer (0.75x) 
                // to stay inside the directional AABB bounds.
                float  stopDist = isRanged ? attackRange : (attackRange * StopDistanceFactor);

                if (!isHoldingPosition && dist > stopDist)
                {
                    float2 baseDir = math.normalizesafe(unitXZ - targetXZ);

                    // Offset angular estable por unidad (golden ratio → distribución uniforme)
                    float angleOffset = (math.frac(entity.Index * 0.618034f) - 0.5f) * math.PI * 0.5f; // ±45°

                    float cosA = math.cos(angleOffset);
                    float sinA = math.sin(angleOffset);
                    float2 rotatedDir = new float2(
                        baseDir.x * cosA - baseDir.y * sinA,
                        baseDir.x * sinA + baseDir.y * cosA
                    );

                    float2 stopXZ = targetXZ + rotatedDir * stopDist;
                    destination   = new float3(stopXZ.x, unitPos.y, stopXZ.y);
                    usesFormationDestination = false;
                }
                else if (!isHoldingPosition)
                {
                    // Already in attack range — stay at current position
                    destination = unitPos;
                    usesFormationDestination = false;
                }

                // ── Rotation: face target when in close range ────────────────
                // Escribir al GO directamente — EntityVisualSync sincroniza GO→ECS,
                // por lo que escribir a ECS transform sería sobreescrito en el mismo frame.
                float facingRange = SystemAPI.HasComponent<UnitRangedStatsComponent>(entity)
                    ? attackRange + 0.25f  // already set to ranged range above, added tolerance
                    : EngagementRange;
                if (dist <= facingRange)
                {
                    agent.updateRotation = false;
                    float2 dir2D = math.normalizesafe(targetXZ - unitXZ);
                    if (math.lengthsq(dir2D) > 0f
                        && SystemAPI.HasComponent<UnitRotationIntentComponent>(entity))
                    {
                        quaternion combatRot = quaternion.LookRotationSafe(
                            new float3(dir2D.x, 0f, dir2D.y), math.up());
                        var intent = SystemAPI.GetComponentRW<UnitRotationIntentComponent>(entity);
                        if ((int)RotationSource.Combat > intent.ValueRO.priority)
                        {
                            intent.ValueRW.targetRotation = combatRot;
                            intent.ValueRW.priority       = (int)RotationSource.Combat;
                            intent.ValueRW.source         = RotationSource.Combat;
                        }
                    }
                }
                else
                {
                    agent.updateRotation = true;
                }
            }
            else
            {
                // Sin combat target activo, o en HoldPosition — NavMesh maneja rotación.
                agent.updateRotation = true;
            }



            // Without an eligible pursuit target, return to the slot even in combat.
            // Holding units retain their combat target for facing/attacks, not movement.
            if (formState.ValueRO.State == UnitFormationState.Waiting
                && (combatTarget == Entity.Null || isHoldingPosition))
            {
                agent.ResetPath(); // hold until randomized reaction delay expires
                navigation.hasIssuedCommand = false;
            }
            else
            {
                if (!usesFormationDestination
                    && !TrySampleDestination(agent, destination, sampleRadius, out destination))
                {
                    destination = formationDestination;
                    usesFormationDestination = true;
                }

                if (agent.SetDestination(destination))
                {
                    navigation.lastCommandDestination = destination;
                    navigation.hasIssuedCommand = true;
                    navigation.lastCommandWasFormation = usesFormationDestination;
                }
                else if (usesFormationDestination)
                {
                    RejectFormationDestination(agent, requestedSlot, unitPos, ref navigation);
                }
            }

            navigationState.ValueRW = navigation;
        }
    }

    private static float3 ResolveFormationDestination(NavMeshAgent agent, float3 requested,
        float3 current, float sampleRadius, float retryDistance, ref NavAgentComponent navigation)
    {
        if (navigation.formationDestinationFailed
            && math.distancesq(navigation.lastFormationRequest, requested) < math.square(retryDistance))
            return navigation.effectiveFormationDestination;

        navigation.lastFormationRequest = requested;
        navigation.hasEffectiveFormationDestination = true;
        if (TrySampleDestination(agent, requested, sampleRadius, out float3 resolved))
        {
            navigation.effectiveFormationDestination = resolved;
            navigation.formationDestinationFailed = false;
            return resolved;
        }

        RejectFormationDestination(agent, requested, current, ref navigation);
        return current;
    }

    private static void DetectFailedFormationPath(NavMeshAgent agent, float3 requested,
        float3 current, float retryDistance, float arrivalThreshold, ref NavAgentComponent navigation)
    {
        if (!navigation.hasIssuedCommand || !navigation.lastCommandWasFormation
            || agent.pathPending
            || math.distancesq(navigation.lastFormationRequest, requested) >= math.square(retryDistance))
            return;

        bool isFar = math.distancesq(current, navigation.lastCommandDestination)
            > math.square(arrivalThreshold);
        if (agent.pathStatus != NavMeshPathStatus.PathComplete || (!agent.hasPath && isFar))
            RejectFormationDestination(agent, requested, current, ref navigation);
    }

    private static void RejectFormationDestination(NavMeshAgent agent, float3 requested,
        float3 current, ref NavAgentComponent navigation)
    {
        navigation.lastFormationRequest = requested;
        navigation.effectiveFormationDestination = current;
        navigation.hasEffectiveFormationDestination = true;
        navigation.formationDestinationFailed = true;
        navigation.hasIssuedCommand = false;
        agent.ResetPath();
    }

    public static bool TrySampleDestination(NavMeshAgent agent, float3 requested,
        float sampleRadius, out float3 resolved)
    {
        var filter = new NavMeshQueryFilter
        {
            agentTypeID = agent.agentTypeID,
            areaMask = agent.areaMask
        };
        if (NavMesh.SamplePosition(requested, out NavMeshHit hit,
                math.max(0.01f, sampleRadius), filter))
        {
            resolved = hit.position;
            return true;
        }

        resolved = default;
        return false;
    }

    /// <summary>Movement permission; target acquisition and attacking remain separate.</summary>
    public static bool CanPursueTarget(SquadOrderType order, float3 slot, float3 target, float leashDistance)
    {
        if (order == SquadOrderType.HoldPosition)
            return false;
        return order == SquadOrderType.Attack
            || math.distancesq(slot, target) <= math.square(math.max(0f, leashDistance));
    }
}
