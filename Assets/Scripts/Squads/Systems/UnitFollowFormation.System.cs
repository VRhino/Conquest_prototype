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
        const float stoppingDistanceSq = 0.04f; // ~0.2m

        float dt = SystemAPI.Time.DeltaTime;

        var slotLookup = GetComponentLookup<UnitGridSlotComponent>(true);
        var targetLookup = GetComponentLookup<UnitLocalTargetComponent>();
        var transformLookup = GetComponentLookup<LocalTransform>();
        var ownerLookup = GetComponentLookup<SquadOwnerComponent>(true);
        var prevLeaderPosLookup = GetComponentLookup<UnitPrevLeaderPosComponent>();
        var stateLookup = GetComponentLookup<SquadStateComponent>(true);
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

            // Obtener el estado de movimiento del héroe
            bool heroMovingForSquad = false;
            if (SystemAPI.HasComponent<HeroStateComponent>(leader))
            {
                var heroState = SystemAPI.GetComponent<HeroStateComponent>(leader);
                heroMovingForSquad = heroState.State == HeroState.Moving;
            }

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

                // Solo procesar movimiento si el héroe está fuera del radio O si la unidad ya está en movimiento
                if (!SystemAPI.HasComponent<UnitFormationStateComponent>(unit))
                {
                    continue;
                }
                    
                var stateComp = SystemAPI.GetComponent<UnitFormationStateComponent>(unit);

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

                // Read target position already calculated by GridFormationUpdateSystem
                if (!SystemAPI.HasComponent<UnitTargetPositionComponent>(unit))
                    continue;
                float3 desired = SystemAPI.GetComponent<UnitTargetPositionComponent>(unit).position;

                float3 current = transformLookup[unit].Position;
                float3 diff = desired - current;
                float distSq = math.lengthsq(diff);

                // Calculate slot position using unified calculator
                float3 slotPos = gridSlot.worldOffset;
                
                // Only move if the unit state is Moving (state management is UnitFormationStateSystem's responsibility)
                bool shouldMove = false;

                // Usar el estado de movimiento del héroe en vez de comparar posiciones
                if (isHoldingPosition)
                {
                    // En Hold Position: una vez que está Moving, usar threshold normal para llegar precisamente
                    shouldMove = stateComp.State == UnitFormationState.Moving && distSq > stoppingDistanceSq;
                }
                else
                {
                    // Si el héroe se está moviendo, la unidad siempre se mueve (sin importar distancia al slot)
                    // Si el héroe está quieto, solo se mueve si está lejos del slot
                    if (heroMovingForSquad)
                        shouldMove = stateComp.State == UnitFormationState.Moving;
                    else
                        shouldMove = stateComp.State == UnitFormationState.Moving && distSq > stoppingDistanceSq;
                }
                
                if (shouldMove)
                {
                    // Obtener la velocidad base de la unidad (ya incluye escalado por nivel y multiplicador de peso)
                    // El cálculo se realiza centralizadamente en UnitSpeedCalculator.CalculateFinalSpeed()
                    float baseSpeed = defaultMoveSpeed; // Fallback
                    if (unitStatsLookup.HasComponent(unit))
                    {
                        baseSpeed = unitStatsLookup[unit].speed;
                    }
                    
                    // Aplicar solo multiplicadores adicionales específicos de la unidad
                    float speedMultiplier = 1f;
                    if (SystemAPI.HasComponent<UnitMoveSpeedVariation>(unit))
                        speedMultiplier = SystemAPI.GetComponent<UnitMoveSpeedVariation>(unit).speedMultiplier;
                    // Si hurryToComander está activo, duplicar el multiplicador de velocidad
                    if (hurryToComander)
                        speedMultiplier *= 2f;

                    float finalSpeed = baseSpeed * speedMultiplier;
                    
                    float3 step = math.normalizesafe(diff) * finalSpeed * dt;
                    if (math.lengthsq(step) > distSq)
                        step = diff;

                    current += step;
                    var t = transformLookup[unit];
                    t.Position = current;

                    // Actualizar orientación basada en configuración
                    UpdateUnitOrientation(unit, ref t, heroPosition, heroForward, diff, dt, UnitOrientationType.FaceMovementDirection);
                    
                    transformLookup[unit] = t;
                    
                    // State management is handled by UnitFormationStateSystem
                    // This system only handles movement
                }
                else
                {
                    // Unidad no se mueve — si está en HoldPosition y Formed, orientarse según holdRotation
                    if (isHoldingPosition && stateComp.State == UnitFormationState.Formed
                        && SystemAPI.HasComponent<SquadHoldPositionComponent>(entity))
                    {
                        var holdComp = SystemAPI.GetComponent<SquadHoldPositionComponent>(entity);
                        var t = transformLookup[unit];

                        float3 holdForward = math.mul(holdComp.holdRotation, math.forward());
                        float3 horizontalDir = math.normalizesafe(new float3(holdForward.x, 0, holdForward.z));

                        if (math.lengthsq(horizontalDir) > 0.01f)
                        {
                            quaternion targetRot = quaternion.LookRotationSafe(horizontalDir, math.up());
                            float rotSpeed = 5f;
                            if (SystemAPI.HasComponent<UnitOrientationComponent>(unit))
                                rotSpeed = SystemAPI.GetComponent<UnitOrientationComponent>(unit).rotationSpeed;
                            t.Rotation = math.slerp(t.Rotation, targetRot, dt * rotSpeed);
                        }

                        transformLookup[unit] = t;
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
