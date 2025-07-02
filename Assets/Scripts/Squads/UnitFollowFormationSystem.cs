using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

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

            Entity leader = ownerLookup[entity].hero;
            if (!transformLookup.HasComponent(leader))
                continue;

            float3 leaderPos = transformLookup[leader].Position;
            LocalTransform leaderTransform = SystemAPI.GetComponent<LocalTransform>(leader);

            // Calculate formation base using unified calculator
            float3 formationBase = FormationPositionCalculator.CalculateFormationBase(leaderTransform, useHeroForward: true);
            
            // Keep heroForward for orientation updates
            float3 heroForward = math.forward(leaderTransform.Rotation);

            // Calcular centro de la squad (promedio de posiciones de las unidades)
            float3 squadCenter = float3.zero;
            int squadCount = 0;
            for (int j = 0; j < units.Length; j++)
            {
                Entity u = units[j].Value;
                if (transformLookup.HasComponent(u))
                {
                    squadCenter += transformLookup[u].Position;
                    squadCount++;
                }
            }
            if (squadCount > 0)
                squadCenter /= squadCount;

            float heroDistSq = math.lengthsq(leaderPos - squadCenter);
            bool heroOutsideRadius = heroDistSq > 25f; // 5 metros al cuadrado

            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i].Value;
                if (!slotLookup.HasComponent(unit) ||
                    !transformLookup.HasComponent(unit))
                    continue;

                // Obtener y actualizar posición previa del líder para esta unidad
                float3 prevLeaderPos = leaderPos;
                if (prevLeaderPosLookup.HasComponent(unit))
                    prevLeaderPos = prevLeaderPosLookup[unit].value;
                prevLeaderPosLookup[unit] = new UnitPrevLeaderPosComponent { value = leaderPos };

                // Get unit's grid slot
                var gridSlot = slotLookup[unit];
                
                // Use unified position calculator for consistency
                float3 desired = FormationPositionCalculator.CalculateDesiredPositionWithBase(
                    formationBase, gridSlot, adjustForTerrain: false);
                
                // Solo usar UnitTargetPositionComponent como override temporal si existe y es diferente
                if (SystemAPI.HasComponent<UnitTargetPositionComponent>(unit))
                {
                    float3 staticTarget = SystemAPI.GetComponent<UnitTargetPositionComponent>(unit).position;
                    float3 dynamicTarget = desired;
                    
                    // Si la posición estática está muy lejos de la dinámica, usar la dinámica (héroe se movió)
                    float distanceBetweenTargets = math.distance(staticTarget, dynamicTarget);
                    if (distanceBetweenTargets > 1f) // Si el héroe se movió más de 1 metro
                    {
                        desired = dynamicTarget; // Seguir al héroe
                        // Actualizar el target component para la próxima vez
                        var targetComp = SystemAPI.GetComponentRW<UnitTargetPositionComponent>(unit);
                        targetComp.ValueRW.position = dynamicTarget;
                    }
                    else
                    {
                        desired = staticTarget; // Mantener formación específica
                    }
                }
                float3 current = transformLookup[unit].Position;
                float3 diff = desired - current;
                float distSq = math.lengthsq(diff);

                // --- NUEVA LÓGICA BASADA EN ESTADO PERSISTENTE ---
                if (!SystemAPI.HasComponent<UnitFormationStateComponent>(unit))
                    continue;
                    
                var stateComp = SystemAPI.GetComponent<UnitFormationStateComponent>(unit);
                
                // Calculate slot position using unified calculator
                float3 slotPos = gridSlot.worldOffset;
                
                // Only move if the unit state is Moving
                if (stateComp.State == UnitFormationState.Moving && distSq > stoppingDistanceSq)
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
                    UpdateUnitOrientation(unit, ref t, leaderPos, heroForward, diff, dt);
                    
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
            quaternion targetRotation = quaternion.LookRotationSafe(targetDirection, math.up());
            transform.Rotation = math.slerp(transform.Rotation, targetRotation, deltaTime * rotationSpeed);
        }
    }
}
