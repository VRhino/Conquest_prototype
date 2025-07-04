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
[UpdateAfter(typeof(FormationSystem))]
public partial class UnitFollowFormationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Dependency.Complete();
        const float defaultMoveSpeed = 5f; // Fallback en caso de que no haya UnitStatsComponent
        const float stoppingDistanceSq = 0.04f; // ~0.2m

        float dt = SystemAPI.Time.DeltaTime;

        var slotLookup = GetComponentLookup<UnitGridSlotComponent>(true);
        var targetLookup = GetComponentLookup<UnitLocalTargetComponent>();
        var transformLookup = GetComponentLookup<LocalTransform>();
        var ownerLookup = GetComponentLookup<SquadOwnerComponent>(true);
        var prevLeaderPosLookup = GetComponentLookup<UnitPrevLeaderPosComponent>();
        var stateLookup = GetComponentLookup<SquadStateComponent>(true);
        var squadDataLookup = GetComponentLookup<SquadDataComponent>(true);
        var unitStatsLookup = GetComponentLookup<UnitStatsComponent>(true);
        
        // TODO: Las unidades pueden usar EnvironmentAwarenessComponent del escuadrón 
        // para adaptar su navegación individual (evitar obstáculos, ajustar velocidad, etc.)
        // var environmentLookup = GetComponentLookup<EnvironmentAwarenessComponent>(true);

        var ecb = new EntityCommandBuffer(Allocator.Temp);
        bool heroIsMoving = false;
        float3 prevHeroPos = float3.zero;
        // Detectar si el héroe se mueve (solo ejemplo, idealmente cachear en otro sistema)
        // Aquí asumimos que si el líder se mueve más de 0.05m, está en movimiento
        // (esto puede mejorarse con un sistema dedicado)

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

            if (!squadDataLookup.TryGetComponent(entity, out var squadData))
            {
                Debug.LogWarning($"[UnitFollowFormationSystem] Squad {entity.Index} no tiene SquadDataComponent");
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

            // Get current formation gridPositions from squad data
            ref BlobArray<int2> gridPositions = ref squadData.formationLibrary.Value.formations[0].gridPositions;
            if (squadData.formationLibrary.IsCreated)
            {
                ref var formations = ref squadData.formationLibrary.Value.formations;
                FormationType currentFormation = squadState.currentFormation;
                
                // Find the current formation in the library
                for (int f = 0; f < formations.Length; f++)
                {
                    if (formations[f].formationType == currentFormation)
                    {
                        gridPositions = ref formations[f].gridPositions;
                        break;
                    }
                }
            }

            // Determinar el comportamiento según el estado del escuadrón
            bool isHoldingPosition = squadState.currentState == SquadFSMState.HoldingPosition;

            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i].Value;
                if (!slotLookup.HasComponent(unit) ||
                    !transformLookup.HasComponent(unit))
                    continue;

                // Solo procesar movimiento si el héroe está fuera del radio O si la unidad ya está en movimiento
                if (!SystemAPI.HasComponent<UnitFormationStateComponent>(unit))
                {
                    continue;
                }
                    
                var stateComp = SystemAPI.GetComponent<UnitFormationStateComponent>(unit);
                
                // Only process movement based on unit's current state
                // State management is handled by UnitFormationStateSystem
                if (isHoldingPosition)
                {
                    // En Hold Position: las unidades NO siguen al héroe, mantienen posición
                    // Solo se mueven si están muy lejos de su slot asignado (por ejemplo, fueron empujadas)
                    // Continuar procesamiento para mantener formación en posición fija
                }

                // Obtener y actualizar posición previa del líder para esta unidad
                float3 prevLeaderPos = heroPosition;
                if (prevLeaderPosLookup.HasComponent(unit))
                {
                    prevLeaderPos = prevLeaderPosLookup[unit].value;
                    prevLeaderPosLookup[unit] = new UnitPrevLeaderPosComponent { value = heroPosition };
                }
                else
                {
                    // Add the component using ECB if it doesn't exist
                    ecb.AddComponent(unit, new UnitPrevLeaderPosComponent { value = heroPosition });
                }

                // Get unit's grid slot
                var gridSlot = slotLookup[unit];
                
                // Use unified position calculator for consistency
                float3 desired = float3.zero;
                FormationPositionCalculator.CalculateDesiredPosition(
                    unit,
                    ref gridPositions,
                    i, // unitIndex
                    squadState,
                    SystemAPI.HasComponent<SquadHoldPositionComponent>(entity) ? SystemAPI.GetComponent<SquadHoldPositionComponent>(entity) : (SquadHoldPositionComponent?)null,
                    heroPosition,
                    out int2 originalGridPos,
                    out float3 gridOffset,
                    out float3 worldPos,
                    true);
                desired = worldPos;

                float3 current = transformLookup[unit].Position;
                float3 diff = desired - current;
                float distSq = math.lengthsq(diff);

                // Calculate slot position using unified calculator
                float3 slotPos = gridSlot.worldOffset;
                
                // Only move if the unit state is Moving (state management is UnitFormationStateSystem's responsibility)
                bool shouldMove = false;
                
                if (isHoldingPosition)
                {
                    // En Hold Position: una vez que está Moving, usar threshold normal para llegar precisamente
                    // El threshold grande solo se usa en UnitFormationStateSystem para detectar cambios de formación
                    shouldMove = stateComp.State == UnitFormationState.Moving && distSq > stoppingDistanceSq;
                }
                else
                {
                    // Comportamiento normal de seguimiento
                    shouldMove = stateComp.State == UnitFormationState.Moving && distSq > stoppingDistanceSq;
                }
                
                if (shouldMove)
                {
                    // Obtener la velocidad base de la unidad (ya incluye escalado por nivel y multiplicador de peso)
                    // El cálculo se realiza centralizadamente en UnitSpeedCalculator.CalculateFinalSpeed()
                    float baseSpeed = defaultMoveSpeed; // Fallback
                    if (unitStatsLookup.HasComponent(unit))
                    {
                        baseSpeed = unitStatsLookup[unit].velocidad;
                    }
                    
                    // Aplicar solo multiplicadores adicionales específicos de la unidad
                    float speedMultiplier = 1f;
                    if (SystemAPI.HasComponent<UnitMoveSpeedVariation>(unit))
                        speedMultiplier = SystemAPI.GetComponent<UnitMoveSpeedVariation>(unit).speedMultiplier;
                    
                    float finalSpeed = baseSpeed * speedMultiplier;
                    float3 step = math.normalizesafe(diff) * finalSpeed * dt;
                    if (math.lengthsq(step) > distSq)
                        step = diff;

                    current += step;
                    var t = transformLookup[unit];
                    t.Position = current;
                    
                    // Actualizar orientación basada en configuración
                    UpdateUnitOrientation(unit, ref t, heroPosition, heroForward, diff, dt);
                    
                    transformLookup[unit] = t;
                    
                    // State management is handled by UnitFormationStateSystem
                    // This system only handles movement
                }
                else
                {
                    // Unit not moving - state management handled automatically
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
    private void UpdateUnitOrientation(Entity unit, ref LocalTransform transform, float3 heroPos, float3 heroForward, float3 movementDirection, float deltaTime)
    {
        // Obtener configuración de orientación (valores por defecto si no existe)
        UnitOrientationType orientationType = UnitOrientationType.MatchHeroDirection;
        float rotationSpeed = 5f;
        
        if (SystemAPI.HasComponent<UnitOrientationComponent>(unit))
        {
            var orientationComp = SystemAPI.GetComponent<UnitOrientationComponent>(unit);
            orientationType = orientationComp.orientationType;
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
