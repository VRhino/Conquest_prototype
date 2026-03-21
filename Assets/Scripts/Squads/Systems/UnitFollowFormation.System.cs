using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Unity.Collections;

/// <summary>
/// Moves each unit of a squad towards its assigned formation slot relative to
/// the squad leader.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(GridFormationUpdateSystem))]
public partial class UnitFollowFormationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Dependency.Complete();
        const float defaultMoveSpeed = 5f; // Fallback en caso de que no haya UnitStatsComponent

        float dt = SystemAPI.Time.DeltaTime;

        var slotLookup = GetComponentLookup<UnitGridSlotComponent>(true);
        var targetLookup = GetComponentLookup<UnitLocalTargetComponent>();
        var transformLookup = GetComponentLookup<LocalTransform>();
        var ownerLookup = GetComponentLookup<SquadOwnerComponent>(true);
        var prevLeaderPosLookup = GetComponentLookup<UnitPrevLeaderPosComponent>();
        var stateLookup = GetComponentLookup<SquadStateComponent>(true);
        
        // TODO: Las unidades pueden usar EnvironmentAwarenessComponent del escuadrón 
        // para adaptar su navegación individual (evitar obstáculos, ajustar velocidad, etc.)
        // var environmentLookup = GetComponentLookup<EnvironmentAwarenessComponent>(true);

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (units, entity) in SystemAPI.Query<DynamicBuffer<SquadUnitElement>>().WithEntityAccess())
        {
            if (units.Length == 0)
                continue;

            if (!ownerLookup.HasComponent(entity))
            {
                Debug.LogWarning($"[UnitFollowFormationSystem] Squad {entity.Index} no tiene SquadOwnerComponent");
                continue;
            }
            
                
            if (!stateLookup.TryGetComponent(entity, out var squadState))
            {
                Debug.LogWarning($"[UnitFollowFormationSystem] Squad {entity.Index} no tiene SquadStateComponent");
                continue;
            }

            Entity leader = ownerLookup[entity].hero;
            if (!transformLookup.HasComponent(leader))
            {
                Debug.LogWarning($"[UnitFollowFormationSystem] Hero {leader.Index} del squad {entity.Index} no tiene LocalTransform o no existe");
                continue;
            }

            float3 heroPosition = transformLookup[leader].Position;
            LocalTransform leaderTransform = SystemAPI.GetComponent<LocalTransform>(leader);
            
            // Keep heroForward for orientation updates
            float3 heroForward = math.forward(leaderTransform.Rotation);

            // Determinar el comportamiento según el estado del escuadrón
            bool isHoldingPosition = squadState.currentState == SquadFSMState.HoldingPosition;

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

                // Mantener posición previa del líder actualizada (leída por otros sistemas)
                if (prevLeaderPosLookup.HasComponent(unit))
                    prevLeaderPosLookup[unit] = new UnitPrevLeaderPosComponent { value = heroPosition };
                else
                    ecb.AddComponent(unit, new UnitPrevLeaderPosComponent { value = heroPosition });

                // Get unit's grid slot
                var gridSlot = slotLookup[unit];

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
                        if (math.lengthsq(horizontalDir) > 0.01f)
                        {
                            quaternion targetRot = quaternion.LookRotationSafe(horizontalDir, math.up());
                            float rotSpeed = 5f;
                            if (SystemAPI.HasComponent<UnitOrientationComponent>(unit))
                                rotSpeed = SystemAPI.GetComponent<UnitOrientationComponent>(unit).rotationSpeed;

                            navAgent.updateRotation = false;
                            Quaternion currentRot = navAgent.transform.rotation;
                            navAgent.transform.rotation = Quaternion.Slerp(currentRot, targetRot, dt * rotSpeed);
                        }
                    }
                }

                // Update visual target regardless of state for UI purposes
                if (targetLookup.HasComponent(unit))
                {
                    var target = targetLookup[unit];
                    target.targetPosition = slotPos;
                    targetLookup[unit] = target;
                }
            }
        }
        
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }

    /// <summary>
    /// Actualiza la orientación de una unidad basada en su configuración de orientación.
    /// </summary>
    private void UpdateUnitOrientation(Entity unit, ref LocalTransform transform, float3 heroPos, float3 heroForward, float3 movementDirection, float deltaTime, UnitOrientationType orientationType)
    {
        float rotationSpeed = 5f;
        
        if (SystemAPI.HasComponent<UnitOrientationComponent>(unit))
        {
            var orientationComp = SystemAPI.GetComponent<UnitOrientationComponent>(unit);
            rotationSpeed = orientationComp.rotationSpeed;
        }

        float3 targetDirection = float3.zero;
        bool shouldRotate = true;

        switch (orientationType)
        {
            case UnitOrientationType.None:
                shouldRotate = false;
                break;

            case UnitOrientationType.FaceHero:
                targetDirection = math.normalizesafe(heroPos - transform.Position);
                break;

            case UnitOrientationType.MatchHeroDirection:
                // Usar la dirección del héroe que ya tenemos
                targetDirection = heroForward;
                break;

            case UnitOrientationType.FaceMovementDirection:
                targetDirection = math.normalizesafe(movementDirection);
                break;
        }

        if (shouldRotate && math.lengthsq(targetDirection) > 0.01f)
        {
            // Limitar la rotación solo al plano horizontal (eliminar componente Y)
            float3 horizontalDirection = new float3(targetDirection.x, 0, targetDirection.z);
            horizontalDirection = math.normalizesafe(horizontalDirection);
            
            // Solo rotar si hay suficiente componente horizontal
            if (math.lengthsq(horizontalDirection) > 0.01f)
            {
                quaternion targetRotation = quaternion.LookRotationSafe(horizontalDirection, math.up());
                transform.Rotation = math.slerp(transform.Rotation, targetRotation, deltaTime * rotationSpeed);
            }
        }
    }
}
