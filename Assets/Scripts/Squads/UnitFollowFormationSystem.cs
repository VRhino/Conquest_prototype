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
        const float moveSpeed = 5f;
        const float stoppingDistanceSq = 0.04f; // ~0.2m

        float dt = SystemAPI.Time.DeltaTime;

        var slotLookup = GetComponentLookup<UnitGridSlotComponent>(true);
        var targetLookup = GetComponentLookup<UnitLocalTargetComponent>();
        var transformLookup = GetComponentLookup<LocalTransform>();
        var ownerLookup = GetComponentLookup<SquadOwnerComponent>(true);
        var prevLeaderPosLookup = GetComponentLookup<UnitPrevLeaderPosComponent>();
        var stateLookup = GetComponentLookup<SquadStateComponent>(true);
        var squadDataLookup = GetComponentLookup<SquadDataComponent>(true);
        
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
                continue;
            
                
            if (!stateLookup.TryGetComponent(entity, out var squadState))
                continue;

            if (!squadDataLookup.TryGetComponent(entity, out var squadData))
                continue;

            Entity leader = ownerLookup[entity].hero;
            if (!transformLookup.HasComponent(leader))
                continue;

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
            bool isFollowingHero = squadState.currentState == SquadFSMState.FollowingHero;
            
            // Calcular si el héroe está fuera del rango del escuadrón
            bool heroOutsideRadius = !FormationPositionCalculator.isHeroInRange(units, transformLookup, heroPosition, 25.0f); // 5m squared radius

            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i].Value;
                if (!slotLookup.HasComponent(unit) ||
                    !transformLookup.HasComponent(unit))
                    continue;

                // Solo procesar movimiento si el héroe está fuera del radio O si la unidad ya está en movimiento
                if (!SystemAPI.HasComponent<UnitFormationStateComponent>(unit))
                    continue;
                    
                var stateComp = SystemAPI.GetComponent<UnitFormationStateComponent>(unit);
                
                // Comportamiento diferente según el estado del escuadrón
                if (isHoldingPosition)
                {
                    // En Hold Position: las unidades NO siguen al héroe, mantienen posición
                    // Solo se mueven si están muy lejos de su slot asignado (por ejemplo, fueron empujadas)
                    // Continuar procesamiento para mantener formación en posición fija
                }
                else if (isFollowingHero)
                {
                    // En Follow Hero: comportamiento normal de seguimiento
                    // Si el héroe está dentro del radio y la unidad ya está formada, no hacer nada
                    if (!heroOutsideRadius && stateComp.State == UnitFormationState.Formed)
                        continue;
                }
                else
                {
                    // Otros estados: seguir comportamiento por defecto
                    if (!heroOutsideRadius && stateComp.State == UnitFormationState.Formed)
                        continue;
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
                
                if (isHoldingPosition)
                {
                    // En Hold Position: usar el centro fijo del escuadrón
                    float3 holdCenter = heroPosition; // Default fallback
                    
                    // Obtener o crear el centro fijo para Hold Position
                    if (SystemAPI.HasComponent<SquadHoldPositionComponent>(entity))
                    {
                        var holdComponent = SystemAPI.GetComponent<SquadHoldPositionComponent>(entity);
                        holdCenter = holdComponent.holdCenter;
                    }
                    else
                    {
                        // Primera vez que se da Hold Position, guardar el centro actual
                        holdCenter = heroPosition;
                        ecb.AddComponent(entity, new SquadHoldPositionComponent 
                        { 
                            holdCenter = holdCenter,
                            originalFormation = squadState.currentFormation
                        });
                    }
                    
                    // Calcular posición de formación usando el centro fijo
                    if (gridPositions.Length > 0 && i < gridPositions.Length)
                    {
                        FormationPositionCalculator.CalculateDesiredPosition(
                            unit,
                            ref gridPositions,
                            holdCenter, // Usar centro fijo en lugar de posición del héroe
                            i,
                            out int2 originalGridPos,
                            out float3 gridOffset,
                            out float3 worldPos,
                            true);
                        desired = worldPos;
                    }
                    else
                    {
                        desired = holdCenter + gridSlot.worldOffset;
                    }
                }
                else
                {
                    // Comportamiento normal: seguir al héroe
                    // Limpiar SquadHoldPositionComponent si cambiamos de Hold Position a Follow
                    if (SystemAPI.HasComponent<SquadHoldPositionComponent>(entity))
                    {
                        ecb.RemoveComponent<SquadHoldPositionComponent>(entity);
                    }
                    
                    // Usar la posición actual del héroe como centro dinámico
                    if (gridPositions.Length > 0 && i < gridPositions.Length)
                    {
                        // Use the actual grid positions from current formation
                        FormationPositionCalculator.CalculateDesiredPosition(
                            unit,
                            ref gridPositions,
                            heroPosition, // Usar posición actual del héroe como centro dinámico
                            i,
                            out int2 originalGridPos,
                            out float3 gridOffset,
                            out float3 worldPos,
                            true);
                        desired = worldPos; // Use calculated world position
                    }
                    else
                    {
                        Debug.LogWarning($"Unit {unit} has no grid position assigned in formation. Using default offset.");
                        // Fallback to grid slot offset if no formation data available
                        desired = heroPosition + gridSlot.worldOffset;
                    }
                }
                float3 current = transformLookup[unit].Position;
                float3 diff = desired - current;
                float distSq = math.lengthsq(diff);

                // Calculate slot position using unified calculator
                float3 slotPos = gridSlot.worldOffset;
                
                // Only move if the unit state is Moving
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
                    float speedMultiplier = 1f;
                    if (SystemAPI.HasComponent<UnitMoveSpeedVariation>(unit))
                        speedMultiplier = SystemAPI.GetComponent<UnitMoveSpeedVariation>(unit).speedMultiplier;
                        
                    float pesoMultiplier = 1f;
                    if (SystemAPI.HasComponent<UnitStatsComponent>(unit))
                    {
                        int peso = SystemAPI.GetComponent<UnitStatsComponent>(unit).peso;
                        if (peso == 2) pesoMultiplier = 0.8f;
                        else if (peso == 3) pesoMultiplier = 0.6f;
                    }
                    speedMultiplier *= pesoMultiplier;

                    float3 step = math.normalizesafe(diff) * moveSpeed * speedMultiplier * dt;
                    if (math.lengthsq(step) > distSq)
                        step = diff;

                    current += step;
                    var t = transformLookup[unit];
                    t.Position = current;
                    
                    // Actualizar orientación basada en configuración
                    UpdateUnitOrientation(unit, ref t, heroPosition, heroForward, diff, dt);
                    
                    transformLookup[unit] = t;
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
        UnitOrientationType orientationType = UnitOrientationType.FaceHero;
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
