using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Assigns target positions to squad units based on the selected formation.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class FormationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        var ownerLookup = GetComponentLookup<SquadOwnerComponent>(true);
        var transformLookup = GetComponentLookup<LocalTransform>(true);

        foreach (var (input, state, squadData, units, squadEntity) in SystemAPI
                    .Query<RefRO<SquadInputComponent>,
                            RefRW<SquadStateComponent>,
                            RefRO<SquadDataComponent>,
                            DynamicBuffer<SquadUnitElement>>()
                    .WithEntityAccess())
        {
            var s = state.ValueRW;
            s.formationChangeCooldown = math.max(0f, s.formationChangeCooldown - deltaTime);

            if (input.ValueRO.desiredFormation == s.currentFormation ||
                s.formationChangeCooldown > 0f)
            {
                state.ValueRW = s;
                continue;
            }

            if (!squadData.ValueRO.formationLibrary.IsCreated || units.Length == 0)
            {
                state.ValueRW = s;
                continue;
            }

            // Obtener la posición del héroe como base de formación
            if (!ownerLookup.TryGetComponent(squadEntity, out var squadOwner))
            {
                state.ValueRW = s;
                continue;
            }
            if (!transformLookup.TryGetComponent(squadOwner.hero, out var heroTransform))
            {
                state.ValueRW = s;
                continue;
            }

            ref var formations = ref squadData.ValueRO.formationLibrary.Value.formations;
            
            // Find the desired formation
            int formationIndex = -1;
            for (int i = 0; i < formations.Length; i++)
            {
                if (formations[i].formationType == input.ValueRO.desiredFormation)
                {
                    formationIndex = i;
                    break;
                }
            }

            if (formationIndex == -1)
            {
                state.ValueRW = s;
                continue;
            }
            //se tiene la formacion deseada
            ref var formation = ref formations[formationIndex];

            int squadUnitCount = units.Length; 
            ref var gridPositions = ref formation.gridPositions;
            int positionsToUse = math.min(squadUnitCount, gridPositions.Length);
            
            for (int i = 0; i < positionsToUse; i++)
            {
                // entidad
                Entity unit = units[i].Value;
                if (!SystemAPI.Exists(unit))
                    continue;
                FormationPositionCalculator.CalculateDesiredPosition(
                    unit, 
                    ref gridPositions,
                    heroTransform.Position, // El FormationSystem siempre usa la posición del héroe como centro
                    i,
                    out int2 originalGridPos,
                    out float3 gridOffset,
                    out float3 worldPos,
                    true);
                
                UpdateUnitPosition(unit, worldPos, new float3(originalGridPos.x, 0, originalGridPos.y), i);
                
                // Update grid slot component - mantener posición original en gridPosition
                if (SystemAPI.HasComponent<UnitGridSlotComponent>(unit))
                {
                    var gridSlot = SystemAPI.GetComponentRW<UnitGridSlotComponent>(unit);
                    gridSlot.ValueRW.gridPosition = originalGridPos; // Mantener posición original
                    gridSlot.ValueRW.worldOffset = gridOffset; // Usar offset directo sin centrado
                }
                else
                {
                    EntityManager.AddComponentData(unit, new UnitGridSlotComponent 
                    { 
                        gridPosition = originalGridPos, // Mantener posición original
                        slotIndex = i,
                        worldOffset = gridOffset // Usar offset directo sin centrado
                    });
                }
            }

            s.currentFormation = input.ValueRO.desiredFormation;
            s.formationChangeCooldown = 1f;
            
            // Si el escuadrón está en Hold Position, actualizar el componente para reflejar la nueva formación
            if (s.currentState == SquadFSMState.HoldingPosition && SystemAPI.HasComponent<SquadHoldPositionComponent>(squadEntity))
            {
                var holdComponent = SystemAPI.GetComponentRW<SquadHoldPositionComponent>(squadEntity);
                holdComponent.ValueRW.originalFormation = input.ValueRO.desiredFormation;
                // Mantener el mismo holdCenter para que la formación se reorganice en el mismo lugar
            }
            
            state.ValueRW = s;
        }
    }

    private void UpdateUnitPosition(Entity unit, float3 worldPos, float3 originalGridPos, int slotIndex)
    {
        // Update UnitTargetPositionComponent
        if (SystemAPI.HasComponent<UnitTargetPositionComponent>(unit))
        {
            var targetPos = SystemAPI.GetComponentRW<UnitTargetPositionComponent>(unit);
            targetPos.ValueRW.position = worldPos;
        }
        else
        {
            EntityManager.AddComponentData(unit, new UnitTargetPositionComponent { position = worldPos });
        }
        
        // Actualizar el campo Slot de UnitSpacingComponent si existe
        if (SystemAPI.HasComponent<UnitSpacingComponent>(unit))
        {
            var spacingComp = SystemAPI.GetComponentRW<UnitSpacingComponent>(unit);
            spacingComp.ValueRW.Slot = new int2((int)originalGridPos.x, (int)originalGridPos.z);
        }
        
        // Mark unit as Moving so it repositions immediately
        if (SystemAPI.HasComponent<UnitFormationStateComponent>(unit))
        {
            var formationState = SystemAPI.GetComponentRW<UnitFormationStateComponent>(unit);
            formationState.ValueRW.State = UnitFormationState.Moving;
            formationState.ValueRW.DelayTimer = 0f;
            formationState.ValueRW.DelayDuration = 0f;
        }
    }

}
