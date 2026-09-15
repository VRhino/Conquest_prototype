using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Applies NavMesh speed and formation-orientation policy after
/// <see cref="UnitNavMeshSystem"/> has selected the physical destination.
/// The historical class name is retained to avoid a broad type/asset migration.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(GridFormationUpdateSystem))]
public partial class UnitFollowFormationSystem : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<MatchStateComponent>();
    }

    protected override void OnUpdate()
    {
        Dependency.Complete();
        const float defaultMoveSpeed = 5f; // Fallback en caso de que no haya UnitStatsComponent

        float dt = SystemAPI.Time.DeltaTime;

        var slotLookup = GetComponentLookup<UnitGridSlotComponent>(true);
        var transformLookup = GetComponentLookup<LocalTransform>();
        var anchorLookup = GetComponentLookup<SquadFormationAnchorComponent>(true);
        var stateLookup = GetComponentLookup<SquadStateComponent>(true);
        var shieldLookup = GetComponentLookup<UnitShieldComponent>(true);
        
        // TODO: Las unidades pueden usar EnvironmentAwarenessComponent del escuadrón 
        // para adaptar su navegación individual (evitar obstáculos, ajustar velocidad, etc.)
        // var environmentLookup = GetComponentLookup<EnvironmentAwarenessComponent>(true);

        foreach (var (units, entity) in SystemAPI.Query<DynamicBuffer<SquadUnitElement>>().WithEntityAccess())
        {
            if (units.Length == 0)
                continue;

            if (!anchorLookup.HasComponent(entity))
                continue;

            if (!stateLookup.TryGetComponent(entity, out var squadState))
            {
                Debug.LogWarning($"[UnitFollowFormationSystem] Squad {entity.Index} no tiene SquadStateComponent");
                continue;
            }

            var anchor = anchorLookup[entity];
            // Keep heroForward for orientation — guard against zero-quaternion sentinel
            float3 heroForward = math.lengthsq(anchor.rotation.value) > 0.01f
                ? math.forward(anchor.rotation)
                : math.forward();

            // Determinar el comportamiento según el estado del escuadrón
            bool isHoldingPosition = squadState.currentOrder == SquadOrderType.HoldPosition
                && squadState.currentState != SquadFSMState.Retreating;

            // Detectar si el squad tiene la bandera hurryToComander activa
            bool hurryToComander = false;
            if (SystemAPI.HasComponent<SquadInputComponent>(entity))
            {
                var squadInput = SystemAPI.GetComponent<SquadInputComponent>(entity);
                hurryToComander = squadInput.hurryToComander;
            }

            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i].Value;
                if (!slotLookup.HasComponent(unit) ||
                    !transformLookup.HasComponent(unit))
                    continue;

                // Unidades con NavMeshAgent son manejadas por UnitNavMeshSystem,
                // pero solo si el agente está activo y en el NavMesh.
                bool navMeshHandlesMovement = false;
                UnityEngine.AI.NavMeshAgent navAgent = null;
                if (SystemAPI.HasComponent<NavAgentComponent>(unit))
                {
                    navAgent = SystemAPI.ManagedAPI.GetComponent<UnityEngine.AI.NavMeshAgent>(unit);
                    if (navAgent != null && navAgent.isOnNavMesh)
                    {
                        navMeshHandlesMovement = true;

                        // Shield break stun: freeze movement
                        if (shieldLookup.HasComponent(unit) && shieldLookup[unit].brokenTimer > 0f)
                        {
                            navAgent.speed = 0f;
                        }
                        else
                        {
                            // Aplicar velocidad NavMesh dinámica (soporta hurryToCommander)
                            float baseSpeed = SystemAPI.HasComponent<UnitStatsComponent>(unit)
                                ? SystemAPI.GetComponent<UnitStatsComponent>(unit).speed
                                : defaultMoveSpeed;
                            float speedMultiplier = SystemAPI.HasComponent<UnitMoveSpeedVariation>(unit)
                                ? SystemAPI.GetComponent<UnitMoveSpeedVariation>(unit).speedMultiplier
                                : 1f;

                            bool hasCombatTarget = SystemAPI.HasComponent<UnitCombatComponent>(unit)
                                && SystemAPI.GetComponent<UnitCombatComponent>(unit).target != Entity.Null
                                && SystemAPI.Exists(SystemAPI.GetComponent<UnitCombatComponent>(unit).target);

                            if (hurryToComander || hasCombatTarget) speedMultiplier *= 2f;
                            navAgent.speed = baseSpeed * speedMultiplier;
                        }
                    }
                    else
                    {
                        // NavMesh no disponible — NO caer a movimiento directo
                        Debug.LogWarning($"[UnitFollowFormation] Unit {unit.Index} has NavAgentComponent but is NOT on NavMesh. Bake the NavMesh surface. Unit will not move.");
                        continue;
                    }
                }

                // Solo procesar movimiento si el héroe está fuera del radio O si la unidad ya está en movimiento
                if (!SystemAPI.HasComponent<UnitFormationStateComponent>(unit))
                {
                    continue;
                }
                    
                var stateComp = SystemAPI.GetComponent<UnitFormationStateComponent>(unit);

                // Read target position already calculated by GridFormationUpdateSystem
                if (!SystemAPI.HasComponent<UnitTargetPositionComponent>(unit))
                    continue;
                float3 slotPos = SystemAPI.GetComponent<UnitTargetPositionComponent>(unit).position;

                // Orientación para unidades Formed — responsabilidad principal de este sistema.
                // Skip si la unidad está en combate: UnitNavMeshSystem ya rotó hacia el target.
                bool isEngaging = SystemAPI.HasComponent<IsEngagingTag>(unit)
                               && SystemAPI.IsComponentEnabled<IsEngagingTag>(unit);
                if (stateComp.State == UnitFormationState.Formed && !isEngaging)
                {
                    float3 targetForward = float3.zero;
                    bool hasTargetOrientation = false;

                    if (isHoldingPosition && SystemAPI.HasComponent<SquadHoldPositionComponent>(entity))
                    {
                        var holdComp = SystemAPI.GetComponent<SquadHoldPositionComponent>(entity);
                        targetForward = math.mul(holdComp.holdRotation, math.forward());
                        hasTargetOrientation = true;
                    }
                    else if (navMeshHandlesMovement)
                    {
                        targetForward = heroForward;
                        hasTargetOrientation = true;
                    }

                    if (hasTargetOrientation)
                    {
                        float3 horizontalDir = math.normalizesafe(new float3(targetForward.x, 0, targetForward.z));
                        if (math.lengthsq(horizontalDir) > 0.01f && navAgent != null
                            && SystemAPI.HasComponent<UnitRotationIntentComponent>(unit))
                        {
                            quaternion targetRot = quaternion.LookRotationSafe(horizontalDir, math.up());
                            float rotSpeed = 5f;
                            if (SystemAPI.HasComponent<UnitOrientationComponent>(unit))
                                rotSpeed = SystemAPI.GetComponent<UnitOrientationComponent>(unit).rotationSpeed;

                            navAgent.updateRotation = false;
                            UnityEngine.Quaternion currentRot = navAgent.transform.rotation;
                            quaternion slerpedRot = math.slerp((quaternion)currentRot, targetRot, dt * rotSpeed);

                            var intent = SystemAPI.GetComponentRW<UnitRotationIntentComponent>(unit);
                            if ((int)RotationSource.Formation > intent.ValueRO.priority)
                            {
                                intent.ValueRW.targetRotation = slerpedRot;
                                intent.ValueRW.priority       = (int)RotationSource.Formation;
                                intent.ValueRW.source         = RotationSource.Formation;
                            }
                        }
                    }
                }

            }
        }
    }
}
