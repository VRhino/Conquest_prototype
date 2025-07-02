using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

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

            ref var formation = ref formations[formationIndex];
            float3 heroPos = heroTransform.Position;
            float3 formationBase = heroPos;

            // Use grid-based positioning - All units in buffer are squad units (hero is separate)
            int squadUnitCount = units.Length; // All units in buffer are squad units
            ref var gridPositions = ref formation.gridPositions;
            int positionsToUse = math.min(squadUnitCount, gridPositions.Length);
            
            for (int i = 0; i < positionsToUse; i++)
            {
                Entity unit = units[i].Value; // Process all units starting from index 0
                if (!SystemAPI.Exists(unit))
                    continue;

                // Get original grid position from blob and use it directly (no centering)
                int2 originalGridPos = gridPositions[i];
                
                // Convert grid position directly to world position
                float3 relativeWorldPos = FormationGridSystem.GridToRelativeWorld(originalGridPos);
                float3 target = formationBase + relativeWorldPos;
                
                UpdateUnitPosition(unit, target, relativeWorldPos, i);
                
                // Update grid slot component - mantener posición original en gridPosition
                if (SystemAPI.HasComponent<UnitGridSlotComponent>(unit))
                {
                    var gridSlot = SystemAPI.GetComponentRW<UnitGridSlotComponent>(unit);
                    gridSlot.ValueRW.gridPosition = originalGridPos; // Mantener posición original
                    gridSlot.ValueRW.worldOffset = relativeWorldPos; // Usar offset directo sin centrado
                }
                else
                {
                    EntityManager.AddComponentData(unit, new UnitGridSlotComponent 
                    { 
                        gridPosition = originalGridPos, // Mantener posición original
                        slotIndex = i,
                        worldOffset = relativeWorldPos // Usar offset directo sin centrado
                    });
                }
            }

            s.currentFormation = input.ValueRO.desiredFormation;
            s.formationChangeCooldown = 1f;
            state.ValueRW = s;
        }
    }

    private void UpdateUnitPosition(Entity unit, float3 target, float3 relativeOffset, int slotIndex)
    {
        // Update UnitTargetPositionComponent
        if (SystemAPI.HasComponent<UnitTargetPositionComponent>(unit))
        {
            var targetPos = SystemAPI.GetComponentRW<UnitTargetPositionComponent>(unit);
            targetPos.ValueRW.position = target;
        }
        else
        {
            EntityManager.AddComponentData(unit, new UnitTargetPositionComponent { position = target });
        }
        // Actualizar el campo Slot de UnitSpacingComponent si existe
        if (SystemAPI.HasComponent<UnitSpacingComponent>(unit))
        {
            var spacingComp = SystemAPI.GetComponentRW<UnitSpacingComponent>(unit);
            int2 slot = (int2)math.round(FormationGridSystem.RelativeWorldToGrid(relativeOffset));
            spacingComp.ValueRW.Slot = slot;
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
