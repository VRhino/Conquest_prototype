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

    protected override void OnCreate()
    {
        _unitToOrder  = new NativeHashMap<Entity, SquadOrderType>(256, Allocator.Persistent);
        _unitToIntent = new NativeHashMap<Entity, TacticalIntent>(256, Allocator.Persistent);
    }

    protected override void OnDestroy()
    {
        if (_unitToOrder.IsCreated)  _unitToOrder.Dispose();
        if (_unitToIntent.IsCreated) _unitToIntent.Dispose();
    }

    protected override void OnUpdate()
    {
        // ── Phase 0: build unit → squad order/intent maps ───────────────────
        _unitToOrder.Clear();
        _unitToIntent.Clear();

        foreach (var (state, units, squadEntity) in
            SystemAPI.Query<RefRO<SquadStateComponent>, DynamicBuffer<SquadUnitElement>>()
                     .WithEntityAccess())
        {
            SquadOrderType order = state.ValueRO.currentOrder;
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

            // Read optional combat components
            bool   hasCombat    = SystemAPI.HasComponent<UnitCombatComponent>(entity)
                                && SystemAPI.HasComponent<UnitWeaponComponent>(entity);
            Entity combatTarget = Entity.Null;
            float  attackRange  = 1.5f; // fallback

            if (hasCombat)
            {
                combatTarget = SystemAPI.GetComponent<UnitCombatComponent>(entity).target;
                attackRange  = SystemAPI.GetComponent<UnitWeaponComponent>(entity).attackRange;

                if (combatTarget != Entity.Null && !SystemAPI.Exists(combatTarget))
                    combatTarget = Entity.Null;

                // Leash: only pursue if enemy is within leashDistance of the unit's formation slot.
                // Skipped when tacticalIntent == Attacking — SquadAI already decided to engage.
                _unitToIntent.TryGetValue(entity, out TacticalIntent tacticalIntent);
                bool isAttacking = tacticalIntent == TacticalIntent.Attacking;

                if (!isAttacking
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
            _unitToOrder.TryGetValue(entity, out SquadOrderType squadOrder);
            bool isHoldingPosition = squadOrder == SquadOrderType.HoldPosition;

            if (combatTarget != Entity.Null
                && SystemAPI.HasComponent<LocalTransform>(combatTarget))
            {
                float3 targetWorldPos = SystemAPI.GetComponent<LocalTransform>(combatTarget).Position;
                float2 unitXZ   = new float2(unitPos.x,        unitPos.z);
                float2 targetXZ = new float2(targetWorldPos.x, targetWorldPos.z);
                float  dist     = math.distance(unitXZ, targetXZ);
                float  stopDist = attackRange * StopDistanceFactor;

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
                if (dist <= EngagementRange)
                {
                    agent.updateRotation = false;
                    float2 dir2D = math.normalizesafe(targetXZ - unitXZ);
                    if (math.lengthsq(dir2D) > 0f)
                    {
                        agent.transform.rotation = UnityEngine.Quaternion.LookRotation(
                            new UnityEngine.Vector3(dir2D.x, 0f, dir2D.y));
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



            // Gate: respect the randomized reaction delay.
            // Combat targets always bypass the delay — units must fight back immediately.
            if (formState.ValueRO.State == UnitFormationState.Waiting && combatTarget == Entity.Null)
            {
                agent.ResetPath(); // hold until delay expires
            }
            else
            {
                agent.SetDestination(destination);
            }
        }
    }
}
