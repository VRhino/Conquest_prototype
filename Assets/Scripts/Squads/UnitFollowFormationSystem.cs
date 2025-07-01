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

        var slotLookup = GetComponentLookup<UnitFormationSlotComponent>(true);
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

            // Calcular punto base de la formación: 5 metros detrás del héroe
            float3 heroForward = math.forward(SystemAPI.GetComponent<LocalTransform>(leader).Rotation);
            float3 formationBase = leaderPos - heroForward * 5f;

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

                // Usar offset de formación (puede haber sido actualizado por FormationSystem)
                float3 offset = slotLookup[unit].relativeOffset;
                
                // Asegurar que el offset esté ajustado a la cuadrícula
                float3 gridOffset = FormationGridSystem.SnapToGrid(offset);
                
                // Calcular posición deseada usando la base de formación (consistente con UnitFormationStateSystem)
                float3 desired = formationBase + gridOffset;
                
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
                
                // Calculate slot position using same logic (snapped to grid)
                float3 slotPos = formationBase + gridOffset;
                
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
}
