using Unity.Collections;
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
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<MatchStateComponent>();
    }

    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (input, state, formationComp, activeFormation, squadDef, units, squadEntity) in SystemAPI
                    .Query<RefRO<SquadInputComponent>,
                            RefRW<SquadStateComponent>,
                            RefRW<FormationComponent>,
                            RefRW<SquadActiveFormationComponent>,
                            RefRO<SquadDefinitionComponent>,
                            DynamicBuffer<SquadUnitElement>>()
                    .WithEntityAccess())
        {
            var s = state.ValueRW;
            s.formationChangeCooldown = math.max(0f, s.formationChangeCooldown - deltaTime);
            activeFormation.ValueRW.formationChangeCooldown = s.formationChangeCooldown;

            if (input.ValueRO.desiredFormation == formationComp.ValueRO.currentFormation ||
                s.formationChangeCooldown > 0f)
            {
                state.ValueRW = s;
                continue;
            }

            if (!squadDef.ValueRO.formationLibrary.IsCreated || units.Length == 0)
            {
                state.ValueRW = s;
                continue;
            }

            // Use the anchor computed by SquadAnchorSystem (handles hold/retreat/follow cases)
            if (!SystemAPI.HasComponent<SquadFormationAnchorComponent>(squadEntity))
            {
                state.ValueRW = s;
                continue;
            }
            float3 heroPosition = SystemAPI.GetComponent<SquadFormationAnchorComponent>(squadEntity).position;

            ref var formations = ref squadDef.ValueRO.formationLibrary.Value.formations;

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

            // Read hold component if squad is in HoldPosition mode
            SquadHoldPositionComponent? holdComponent = null;
            if (SystemAPI.HasComponent<SquadHoldPositionComponent>(squadEntity))
                holdComponent = SystemAPI.GetComponent<SquadHoldPositionComponent>(squadEntity);

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
                    i, // unitIndex
                    state.ValueRW, // SquadStateComponent
                    holdComponent, // SquadHoldPositionComponent? — use actual value instead of null
                    heroPosition, // heroPos
                    out int2 originalGridPos,
                    out float3 gridOffset,
                    out float3 worldPos,
                    true);

                UpdateUnitPosition(unit, worldPos, new float3(originalGridPos.x, 0, originalGridPos.y), i, ecb);

                // Update grid slot component - mantener posición original en gridPosition
                if (SystemAPI.HasComponent<UnitGridSlotComponent>(unit))
                {
                    var gridSlot = SystemAPI.GetComponentRW<UnitGridSlotComponent>(unit);
                    gridSlot.ValueRW.gridPosition = originalGridPos; // Mantener posición original
                    gridSlot.ValueRW.worldOffset = gridOffset; // Usar offset directo sin centrado
                }
                else
                {
                    ecb.AddComponent(unit, new UnitGridSlotComponent
                    {
                        gridPosition = originalGridPos, // Mantener posición original
                        slotIndex = i,
                        worldOffset = gridOffset // Usar offset directo sin centrado
                    });
                }
            }

            formationComp.ValueRW.currentFormation = input.ValueRO.desiredFormation;
            s.formationChangeCooldown = 1f;
            // [Sprint2 dual-write]
            activeFormation.ValueRW.currentFormation      = input.ValueRO.desiredFormation;
            activeFormation.ValueRW.formationChangeCooldown = 1f;

            // Si el escuadrón está en Hold Position, actualizar el componente para reflejar la nueva formación
            if (s.currentState == SquadFSMState.HoldingPosition && SystemAPI.HasComponent<SquadHoldPositionComponent>(squadEntity))
            {
                var holdCompRW = SystemAPI.GetComponentRW<SquadHoldPositionComponent>(squadEntity);
                holdCompRW.ValueRW.originalFormation = input.ValueRO.desiredFormation;
                // Mantener el mismo holdCenter para que la formación se reorganice en el mismo lugar
            }

            state.ValueRW = s;
        }

        // Execute all deferred changes
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }

    private void UpdateUnitPosition(Entity unit, float3 worldPos, float3 originalGridPos, int slotIndex, EntityCommandBuffer ecb)
    {
        // Update UnitTargetPositionComponent
        if (SystemAPI.HasComponent<UnitTargetPositionComponent>(unit))
        {
            var targetPos = SystemAPI.GetComponentRW<UnitTargetPositionComponent>(unit);
            targetPos.ValueRW.position = worldPos;
        }
        else
        {
            ecb.AddComponent(unit, new UnitTargetPositionComponent { position = worldPos });
        }

        // Actualizar el campo Slot de UnitSpacingComponent si existe
        if (SystemAPI.HasComponent<UnitSpacingComponent>(unit))
        {
            var spacingComp = SystemAPI.GetComponentRW<UnitSpacingComponent>(unit);
            spacingComp.ValueRW.Slot = new int2((int)originalGridPos.x, (int)originalGridPos.z);
        }

        // State management is handled by UnitFormationStateSystem
        // This system only handles position calculation
    }

}
