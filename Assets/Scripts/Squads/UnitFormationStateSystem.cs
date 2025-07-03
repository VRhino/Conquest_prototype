using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct UnitFormationStateSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;
        const float formationRadiusSq = 25f; // 5m squared
        const float slotThresholdSq = 0.04f; // ~0.2m threshold for being "in slot" (reducido de 0.25f)

        foreach (var (units, squadEntity) in SystemAPI.Query<DynamicBuffer<SquadUnitElement>>().WithEntityAccess())
        {
            if (!SystemAPI.HasComponent<SquadOwnerComponent>(squadEntity))
                continue;

            // Get squad data and state to access formation library
            if (!SystemAPI.HasComponent<SquadDataComponent>(squadEntity) || 
                !SystemAPI.HasComponent<SquadStateComponent>(squadEntity))
                continue;

            var squadData = SystemAPI.GetComponent<SquadDataComponent>(squadEntity);
            var squadState = SystemAPI.GetComponent<SquadStateComponent>(squadEntity);

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

            Entity hero = SystemAPI.GetComponent<SquadOwnerComponent>(squadEntity).hero;
            if (!SystemAPI.HasComponent<LocalTransform>(hero) || !SystemAPI.HasComponent<HeroStateComponent>(hero))
                continue;

            float3 heroPos = SystemAPI.GetComponent<LocalTransform>(hero).Position;
            var heroState = SystemAPI.GetComponent<HeroStateComponent>(hero).State;

            // Determinar si el escuadrón está en modo Hold Position
            bool isHoldingPosition = squadState.currentState == SquadFSMState.HoldingPosition;

            // Calculate squad center (average position of all units)
            float3 squadCenter = float3.zero;
            bool heroWithinRadius = FormationPositionCalculator.isHeroInRange(units, SystemAPI.GetComponentLookup<LocalTransform>(true), heroPos, formationRadiusSq);
            
            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i].Value;
                if (!SystemAPI.HasComponent<UnitGridSlotComponent>(unit) ||
                    !SystemAPI.HasComponent<LocalTransform>(unit) ||
                    !SystemAPI.HasComponent<UnitFormationStateComponent>(unit))
                    continue;

                var slot = SystemAPI.GetComponent<UnitGridSlotComponent>(unit);
                var stateComp = SystemAPI.GetComponent<UnitFormationStateComponent>(unit);

                // Use unified position calculator with current formation
                float3 desiredSlotPos = float3.zero;
                
                if (isHoldingPosition && SystemAPI.HasComponent<SquadHoldPositionComponent>(squadEntity))
                {
                    // En Hold Position, usar el centro fijo del escuadrón
                    var holdComponent = SystemAPI.GetComponent<SquadHoldPositionComponent>(squadEntity);
                    
                    if (gridPositions.Length > 0 && i < gridPositions.Length)
                    {
                        FormationPositionCalculator.CalculateDesiredPosition(
                            unit,
                            ref gridPositions,
                            holdComponent.holdCenter, // Usar centro fijo en lugar de posición del héroe
                            i,
                            out int2 originalGridPos,
                            out float3 gridOffset,
                            out float3 worldPos,
                            true);
                        desiredSlotPos = worldPos;
                    }
                    else
                    {
                        desiredSlotPos = holdComponent.holdCenter + slot.worldOffset;
                    }
                }
                else if (gridPositions.Length > 0 && i < gridPositions.Length)
                {
                    FormationPositionCalculator.CalculateDesiredPosition(
                        unit,
                        ref gridPositions,
                        heroPos, // Usar posición actual del héroe como centro dinámico
                        i,
                        out int2 originalGridPos,
                        out float3 gridOffset,
                        out float3 worldPos,
                        true);
                    desiredSlotPos = worldPos;
                }
                else
                {
                    // Fallback to grid slot offset if no formation data available
                    desiredSlotPos = heroPos + slot.worldOffset;
                }
                
                float3 currentPos = SystemAPI.GetComponent<LocalTransform>(unit).Position;
                
                // Use unified position checker
                bool inSlot = FormationPositionCalculator.IsUnitInSlot(currentPos, desiredSlotPos, slotThresholdSq);

                // State transition logic
                if (isHoldingPosition)
                {
                    // En Hold Position: transiciones de estado simplificadas
                    // Las unidades solo se mueven si están muy lejos de su posición asignada
                    // Usar un threshold más grande solo para detectar si necesita reorganizarse
                    const float holdPositionThresholdSq = 1.0f; // 1 metro cuadrado para cambios de formación (reducido de 4.0f)
                    
                    switch (stateComp.State)
                    {
                        case UnitFormationState.Formed:
                            // Cambiar a Moving si está fuera de la posición asignada
                            // Usar threshold más grande para permitir cambios de formación
                            if (!FormationPositionCalculator.IsUnitInSlot(currentPos, desiredSlotPos, holdPositionThresholdSq))
                            {
                                stateComp.State = UnitFormationState.Moving;
                                stateComp.DelayTimer = 0f;
                            }
                            break;

                        case UnitFormationState.Waiting:
                            // En Hold Position, pasar directamente a Moving sin delay
                            stateComp.State = UnitFormationState.Moving;
                            stateComp.DelayTimer = 0f;
                            break;

                        case UnitFormationState.Moving:
                            // Moving -> Formed: Unit reaches assigned position
                            // Usar el threshold normal (más estricto) para considerar que llegó
                            if (inSlot)
                            {
                                stateComp.State = UnitFormationState.Formed;
                                stateComp.DelayTimer = 0f;
                            }
                            break;
                    }
                }
                else
                {
                    // Comportamiento normal de seguimiento al héroe
                    // Usar thresholds normales para formaciones precisas
                    switch (stateComp.State)
                    {
                        case UnitFormationState.Formed:
                            // Formed -> Waiting: Hero leaves grid radius
                            if (!heroWithinRadius)
                            {
                                stateComp.State = UnitFormationState.Waiting;
                                stateComp.DelayTimer = 0f;
                                stateComp.DelayDuration = UnityEngine.Random.Range(0.5f, 1.5f);
                            }
                            break;

                        case UnitFormationState.Waiting:
                            // Waiting -> Moving: Random delay expires
                            stateComp.DelayTimer += dt;
                            if (stateComp.DelayTimer >= stateComp.DelayDuration)
                            {
                                stateComp.State = UnitFormationState.Moving;
                                stateComp.DelayTimer = 0f;
                            }
                            // Waiting -> Formed: Hero returns to radius while still waiting
                            else if (heroWithinRadius && inSlot)
                            {
                                stateComp.State = UnitFormationState.Formed;
                                stateComp.DelayTimer = 0f;
                            }
                            break;

                        case UnitFormationState.Moving:
                            // Moving -> Formed: Unit reaches slot AND hero is within radius
                            if (inSlot && heroWithinRadius)
                            {
                                stateComp.State = UnitFormationState.Formed;
                                stateComp.DelayTimer = 0f;
                            }
                            // Note: If hero leaves radius while moving, unit continues moving
                            // and will transition to Waiting only after reaching slot (if hero still out)
                            // or directly to Formed if hero returns to radius when unit reaches slot
                            break;
                    }
                }

                SystemAPI.SetComponent(unit, stateComp);
            }
        }
    }
}
