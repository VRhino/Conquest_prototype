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

    // Unit → squad tactical intent map, rebuilt every frame.
    private NativeHashMap<Entity, TacticalIntent> _unitToIntent;

    // Unit → squad FSM state map, rebuilt every frame.
    private NativeHashMap<Entity, SquadFSMState> _unitToFSMState;

    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<MatchStateComponent>();
        _unitToOrder    = new NativeHashMap<Entity, SquadOrderType>(256, Allocator.Persistent);
        _unitToIntent   = new NativeHashMap<Entity, TacticalIntent>(256, Allocator.Persistent);
        _unitToFSMState = new NativeHashMap<Entity, SquadFSMState>(256, Allocator.Persistent);
    }

    protected override void OnDestroy()
    {
        if (_unitToOrder.IsCreated)    _unitToOrder.Dispose();
        if (_unitToIntent.IsCreated)   _unitToIntent.Dispose();
        if (_unitToFSMState.IsCreated) _unitToFSMState.Dispose();
    }

    protected override void OnUpdate()
    {
        // ── Phase 0: build unit → squad order/intent/state maps ────────────
        _unitToOrder.Clear();
        _unitToIntent.Clear();
        _unitToFSMState.Clear();

        foreach (var (state, units, squadEntity) in
            SystemAPI.Query<RefRO<SquadStateComponent>, DynamicBuffer<SquadUnitElement>>()
                     .WithEntityAccess())
        {
            SquadOrderType order    = state.ValueRO.currentOrder;
            SquadFSMState  fsmState = state.ValueRO.currentState;
            TacticalIntent intent = SystemAPI.HasComponent<SquadAIComponent>(squadEntity)
                ? SystemAPI.GetComponent<SquadAIComponent>(squadEntity).tacticalIntent
                : TacticalIntent.Idle;

            for (int i = 0; i < units.Length; i++)
            {
                Entity u = units[i].Value;
                if (u != Entity.Null)
                {
                    _unitToOrder.TryAdd(u, order);
                    _unitToIntent.TryAdd(u, intent);
                    _unitToFSMState.TryAdd(u, fsmState);
                }
            }
        }

        // Read leash distance once — avoids per-unit singleton lookup
        float leashDistance = SystemAPI.HasSingleton<SquadSpawnConfigComponent>()
            ? SystemAPI.GetSingleton<SquadSpawnConfigComponent>().unitLeashDistance
            : 6f;

        // ── Phase 1: movement + rotation decision per NavMesh unit ───────────
        foreach (var (targetPos, formState, transform, entity) in
            SystemAPI.Query<RefRO<UnitTargetPositionComponent>,
                            RefRO<UnitFormationStateComponent>,
                            RefRW<LocalTransform>>()
                     .WithAll<NavAgentComponent>()
                     .WithEntityAccess())
        {
            var agent = SystemAPI.ManagedAPI.GetComponent<NavMeshAgent>(entity);
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
                continue;

            agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;

            float3 unitPos    = transform.ValueRO.Position;
            float3 destination = targetPos.ValueRO.position; // default: formation slot

            // Read squad state early — needed by leash bypass and movement gate.
            _unitToOrder.TryGetValue(entity, out SquadOrderType squadOrder);
            _unitToFSMState.TryGetValue(entity, out SquadFSMState squadFSMState);

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

                // Leash: only pursue if enemy is within leashDistance of the unit's formation slot.
                // Bypassed when tacticalIntent == Attacking (SquadAI decided to engage)
                // OR when squad FSM is InCombat (unit must pursue target, not return to slot).
                _unitToIntent.TryGetValue(entity, out TacticalIntent tacticalIntent);
                bool isAttacking = tacticalIntent == TacticalIntent.Attacking;
                bool isInCombat  = squadFSMState == SquadFSMState.InCombat;

                if (!isAttacking && !isInCombat
                    && combatTarget != Entity.Null
                    && SystemAPI.HasComponent<LocalTransform>(combatTarget))
                {
                    float3 slotPos    = targetPos.ValueRO.position;
                    float3 enemyPos3D = SystemAPI.GetComponent<LocalTransform>(combatTarget).Position;
                    if (math.distancesq(enemyPos3D, slotPos) > leashDistance * leashDistance)
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

                if (dist > stopDist)
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
                }
                else
                {
                    // Already in attack range — stay at current position
                    destination = unitPos;
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



            // Gate: movement decision based on squad FSM state.
            if (squadFSMState == SquadFSMState.InCombat)
            {
                if (combatTarget != Entity.Null)
                    agent.SetDestination(destination); // SetDestination overrides any previous path naturally
                else
                    agent.ResetPath(); // No target available — stop in place
            }
            else if (formState.ValueRO.State == UnitFormationState.Waiting && combatTarget == Entity.Null)
            {
                agent.ResetPath(); // hold until randomized reaction delay expires
            }
            else
            {
                agent.SetDestination(destination);
            }
        }
    }
}
