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
    private static readonly Unity.Profiling.ProfilerMarker ProjectionMarker =
        new Unity.Profiling.ProfilerMarker("Conquest.NavMeshProjection");
    private static int _projectionFrame = -1;
    private static int _projectionRequests;
    private static int _projectionFailures;
    private static int _setDestinationCalls;
    public static long DebugTotalProjectionRequests { get; private set; }
    public static long DebugTotalProjectionFailures { get; private set; }
    /// <summary>Debug-only count of NavMeshAgent.SetDestination calls (movement metrics).</summary>
    public static long DebugTotalSetDestinationCalls { get; private set; }

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetProjectionCounters()
    {
        _projectionFrame = -1;
        _projectionRequests = 0;
        _projectionFailures = 0;
        _setDestinationCalls = 0;
        DebugTotalProjectionRequests = 0;
        DebugTotalProjectionFailures = 0;
        DebugTotalSetDestinationCalls = 0;
    }

    public static int DebugProjectionRequestsThisFrame
    {
        get { RefreshProjectionCounters(); return _projectionRequests; }
    }

    public static int DebugProjectionFailuresThisFrame
    {
        get { RefreshProjectionCounters(); return _projectionFailures; }
    }

    public static int DebugSetDestinationCallsThisFrame
    {
        get { RefreshProjectionCounters(); return _setDestinationCalls; }
    }

    private static void RefreshProjectionCounters()
    {
        int frame = UnityEngine.Time.frameCount;
        if (_projectionFrame == frame) return;
        _projectionFrame = frame;
        _projectionRequests = 0;
        _projectionFailures = 0;
        _setDestinationCalls = 0;
    }

    /// <summary>Counts a SetDestination call (accepted or not) and passes its result through.</summary>
    private static bool CountSetDestinationAnd(bool accepted)
    {
        RefreshProjectionCounters();
        _setDestinationCalls++;
        DebugTotalSetDestinationCalls++;
        return accepted;
    }

    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<MatchStateComponent>();
    }

    protected override void OnUpdate()
    {
        bool battleActive = SystemAPI.GetSingleton<MatchStateComponent>().currentState == MatchState.InBattle;
        // Read leash distance once — avoids per-unit singleton lookup
        bool hasConfig = SystemAPI.HasSingleton<SquadSpawnConfigComponent>();
        var config = hasConfig ? SystemAPI.GetSingleton<SquadSpawnConfigComponent>() : default;
        float leashDistance = hasConfig ? config.unitLeashDistance : 6f;
        float sampleRadius = hasConfig ? math.max(0.01f, config.navMeshDestinationSampleRadius) : 2f;
        float destinationChangeThreshold = hasConfig && config.navMeshDestinationChangeThreshold > 0f
            ? config.navMeshDestinationChangeThreshold : 0.25f;
        float retryDistance = hasConfig ? math.max(0.01f, config.navMeshFailureRetryDistance) : 1f;
        float retryInterval = hasConfig && config.navMeshFailureRetryInterval > 0f
            ? config.navMeshFailureRetryInterval : 1f;
        double now = SystemAPI.Time.ElapsedTime;
        // Movement tuning is optional here so navigation keeps safe defaults in reduced worlds.
        var tuning = SystemAPI.HasSingleton<MovementTuningComponent>()
            ? SystemAPI.GetSingleton<MovementTuningComponent>() : MovementTuningComponent.Default;
        float arrivalThreshold = math.max(0.01f, tuning.arrivalRadiusIn);
        // Contact distances derive from body radii and weapon reach (C5, MeleeReach).
        float bodyRadius = tuning.bodyRadius;
        // Within this distance a melee unit turns to face its target manually.
        float combatFacingRange = tuning.combatFacingRange;
        float dt = SystemAPI.Time.DeltaTime;
        // Below this slot speed the formation command is the slot itself (no lead).
        float leadSpeedThresholdSq = math.square(hasConfig ? config.anchorMovingSpeedThreshold : 0.1f);

        // Movement + rotation decision per NavMesh unit.
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

            if (!battleActive)
            {
                if (agent.hasPath || agent.pathPending) agent.ResetPath();
                if (!agent.isStopped) agent.isStopped = true;
                navigationState.ValueRW.hasIssuedCommand = false;
                continue;
            }
            if (agent.isStopped) agent.isStopped = false;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;

            float3 unitPos = transform.ValueRO.Position;
            float3 requestedSlot = targetPos.ValueRO.position;
            var navigation = navigationState.ValueRO;
            float3 slotVelocity = FormationDestinationPrediction.UpdateSlotVelocity(
                ref navigation.previousRequestedSlot, ref navigation.hasPreviousRequestedSlot,
                ref navigation.slotVelocity, requestedSlot, dt, tuning.slotVelocitySmoothing,
                2f * tuning.destinationLeadMaxDistance);
            DetectFailedFormationPath(agent, requestedSlot, unitPos, retryDistance,
                arrivalThreshold, now, retryInterval, ref navigation);
            // Project the slot within its own cell first (min(spacing, 0.65), L3-04) so it never lands in a
            // neighbour's slot; the wide radius is only the fallback.
            float slotSampleRadius = math.min(sampleRadius, MaxSlotSampleRadius);
            if (SystemAPI.HasComponent<UnitOwnerComponent>(entity))
            {
                Entity owner = SystemAPI.GetComponent<UnitOwnerComponent>(entity).squad;
                if (SystemAPI.HasComponent<FormationComponent>(owner))
                    slotSampleRadius = math.min(slotSampleRadius,
                        math.max(FormationDefinition.MinSpacing, SystemAPI.GetComponent<FormationComponent>(owner).spacing));
            }
            float3 formationDestination = ResolveFormationDestination(agent, requestedSlot,
                unitPos, slotSampleRadius, sampleRadius, retryDistance, destinationChangeThreshold,
                now, retryInterval, ref navigation);
            float3 destination = formationDestination;
            bool usesFormationDestination = true;

            // Tactical order controls pursuit independently of combat state.
            SquadOrderType squadOrder = default;
            OrderSource orderSource = OrderSource.Player;
            if (SystemAPI.HasComponent<UnitOwnerComponent>(entity))
            {
                Entity squad = SystemAPI.GetComponent<UnitOwnerComponent>(entity).squad;
                if (SystemAPI.Exists(squad) && SystemAPI.HasComponent<SquadStateComponent>(squad))
                    squadOrder = SystemAPI.GetComponent<SquadStateComponent>(squad).currentOrder;
                if (SystemAPI.Exists(squad) && SystemAPI.HasComponent<SquadResolvedOrderComponent>(squad))
                    orderSource = SystemAPI.GetComponent<SquadResolvedOrderComponent>(squad).source;
            }

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
                        && !CanPursueTarget(squadOrder, orderSource, slotPos, enemyPos3D, leashDistance))
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
                
                // Stop with bodies apart and the target inside the hit; approach again only beyond the
                // resume distance, so units do not shuffle in and out at the edge of their reach (C5).
                float stopDist = isRanged
                    ? MeleeReach.RangedStopDistance(attackRange, tuning.rangedStopFraction)
                    : MeleeReach.StopDistance(attackRange, bodyRadius, bodyRadius, tuning.meleeStopReachFraction);
                float resumeDist = isRanged ? attackRange : MeleeReach.ResumeDistance(attackRange, bodyRadius);
                if (dist > resumeDist) navigation.combatApproaching = true;
                else if (dist <= stopDist) navigation.combatApproaching = false;

                if (!isHoldingPosition && navigation.combatApproaching)
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
                    // Within reach — stay at current position
                    destination = unitPos;
                    usesFormationDestination = false;
                }

                // ── Rotation: face target when in close range ────────────────
                // Escribir al GO directamente — EntityVisualSync sincroniza GO→ECS,
                // por lo que escribir a ECS transform sería sobreescrito en el mismo frame.
                float facingRange = SystemAPI.HasComponent<UnitRangedStatsComponent>(entity)
                    ? attackRange + 0.25f  // already set to ranged range above, added tolerance
                    : combatFacingRange;
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
                // A new target is approached until the stop distance.
                navigation.combatApproaching = true;
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
                float commandChangeThreshold = destinationChangeThreshold;
                if (usesFormationDestination)
                {
                    // Lead the moving slot; the arrival check keeps using the real slot (effectiveFormationDestination).
                    commandChangeThreshold = FormationDestinationPrediction.ChangeThreshold(slotVelocity,
                        tuning.destinationChangeMin, tuning.destinationChangeSpeedFactor);
                    if (math.lengthsq(slotVelocity.xz) > leadSpeedThresholdSq && !navigation.formationDestinationFailed)
                    {
                        // Lead the already projected slot: no extra NavMesh projection per command; the agent
                        // paths to the nearest reachable point if the lead leaves the surface.
                        float3 predicted = FormationDestinationPrediction.Predict(formationDestination, slotVelocity,
                            tuning.destinationLeadTime, tuning.destinationLeadMaxDistance);
                        destination = navigation.hasIssuedCommand && navigation.lastCommandWasFormation
                            && math.distancesq(predicted, navigation.lastCommandDestination)
                                < math.square(commandChangeThreshold)
                            ? navigation.lastCommandDestination
                            : predicted;
                    }
                }

                if (!usesFormationDestination
                    && navigation.hasIssuedCommand && !navigation.lastCommandWasFormation
                    && math.distancesq(destination, navigation.lastCommandDestination)
                        < math.square(destinationChangeThreshold))
                    destination = navigation.lastCommandDestination;
                else if (!usesFormationDestination
                    && !TrySampleDestination(agent, destination, sampleRadius, out destination))
                {
                    destination = formationDestination;
                    usesFormationDestination = true;
                }

                bool commandStillValid = navigation.hasIssuedCommand
                    && navigation.lastCommandWasFormation == usesFormationDestination
                    && math.distancesq(destination, navigation.lastCommandDestination)
                        < math.square(commandChangeThreshold)
                    && (agent.pathPending
                        || (agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathComplete)
                        || math.distancesq(unitPos, destination) < math.square(arrivalThreshold));
                if (commandStillValid)
                {
                    // Keep the current path; issuing it again each frame forces NavMesh to recalculate.
                }
                else if (CountSetDestinationAnd(agent.SetDestination(destination)))
                {
                    navigation.lastCommandDestination = destination;
                    navigation.hasIssuedCommand = true;
                    navigation.lastCommandWasFormation = usesFormationDestination;
                }
                else if (usesFormationDestination)
                {
                    RejectFormationDestination(agent, requestedSlot, unitPos, now, retryInterval, ref navigation);
                }
            }

            navigationState.ValueRW = navigation;
        }
    }

    /// <summary>Largest radius used to project a slot onto the NavMesh before the wide fallback.</summary>
    const float MaxSlotSampleRadius = 0.65f;

    private static float3 ResolveFormationDestination(NavMeshAgent agent, float3 requested,
        float3 current, float slotSampleRadius, float fallbackSampleRadius, float retryDistance,
        float destinationChangeThreshold, double now,
        float retryInterval, ref NavAgentComponent navigation)
    {
        if (navigation.formationDestinationFailed
            && math.distancesq(navigation.lastFormationRequest, requested) < math.square(retryDistance)
            && now < navigation.nextFormationRetryTime)
            return navigation.effectiveFormationDestination;

        if (navigation.hasEffectiveFormationDestination && !navigation.formationDestinationFailed
            && math.distancesq(navigation.lastFormationRequest, requested)
                < math.square(destinationChangeThreshold))
            return navigation.effectiveFormationDestination;

        navigation.lastFormationRequest = requested;
        navigation.hasEffectiveFormationDestination = true;
        if (TrySampleDestination(agent, requested, slotSampleRadius, out float3 resolved)
            || (fallbackSampleRadius > slotSampleRadius
                && TrySampleDestination(agent, requested, fallbackSampleRadius, out resolved)))
        {
            navigation.effectiveFormationDestination = resolved;
            navigation.formationDestinationFailed = false;
            navigation.nextFormationRetryTime = 0;
            return resolved;
        }

        RejectFormationDestination(agent, requested, current, now, retryInterval, ref navigation);
        return current;
    }

    private static void DetectFailedFormationPath(NavMeshAgent agent, float3 requested,
        float3 current, float retryDistance, float arrivalThreshold, double now,
        float retryInterval, ref NavAgentComponent navigation)
    {
        if (!navigation.hasIssuedCommand || !navigation.lastCommandWasFormation
            || agent.pathPending
            || math.distancesq(navigation.lastFormationRequest, requested) >= math.square(retryDistance))
            return;

        bool isFar = math.distancesq(current, navigation.lastCommandDestination)
            > math.square(arrivalThreshold);
        if (agent.pathStatus != NavMeshPathStatus.PathComplete || (!agent.hasPath && isFar))
            RejectFormationDestination(agent, requested, current, now, retryInterval, ref navigation);
    }

    private static void RejectFormationDestination(NavMeshAgent agent, float3 requested,
        float3 current, double now, float retryInterval, ref NavAgentComponent navigation)
    {
        navigation.lastFormationRequest = requested;
        navigation.effectiveFormationDestination = current;
        navigation.hasEffectiveFormationDestination = true;
        navigation.formationDestinationFailed = true;
        navigation.nextFormationRetryTime = now + retryInterval;
        navigation.hasIssuedCommand = false;
        agent.ResetPath();
    }

    public static bool TrySampleDestination(NavMeshAgent agent, float3 requested,
        float sampleRadius, out float3 resolved)
    {
        using var projectionSample = ProjectionMarker.Auto();
        RefreshProjectionCounters();
        _projectionRequests++;
        DebugTotalProjectionRequests++;
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
        _projectionFailures++;
        DebugTotalProjectionFailures++;
        return false;
    }

    /// <summary>Movement permission; target acquisition and attacking remain separate.</summary>
    public static bool CanPursueTarget(SquadOrderType order, float3 slot, float3 target, float leashDistance)
        => CanPursueTarget(order, OrderSource.Player, slot, target, leashDistance);

    /// <summary>
    /// Only an Attack the player or the AI ordered pursues freely; an Attack produced by a combat
    /// reaction stays within the leash of the unit's slot (C6).
    /// </summary>
    public static bool CanPursueTarget(SquadOrderType order, OrderSource source, float3 slot, float3 target,
        float leashDistance)
    {
        if (order == SquadOrderType.HoldPosition)
            return false;
        return (order == SquadOrderType.Attack && source != OrderSource.CombatReaction)
            || math.distancesq(slot, target) <= math.square(math.max(0f, leashDistance));
    }
}
