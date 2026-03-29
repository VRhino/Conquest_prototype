using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Sistema que mantiene sincronizados los componentes de grid con las posiciones de formación.
/// Simplificado para trabajar solo con grid-based formations.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(FormationSystem))]
public partial class GridFormationUpdateSystem : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<MatchStateComponent>();
    }

    protected override void OnUpdate()
    {
        // Actualizar posiciones target cuando las unidades cambien de grid slot
        foreach (var (units, squadEntity) in SystemAPI
                    .Query<DynamicBuffer<SquadUnitElement>>()
                    .WithEntityAccess())
        {
            if (units.Length == 0) continue;
            
            // Get squad data and state to access formation library
            if (!SystemAPI.HasComponent<SquadDefinitionComponent>(squadEntity) ||
                !SystemAPI.HasComponent<SquadStateComponent>(squadEntity))
                continue;

            var squadDef  = SystemAPI.GetComponent<SquadDefinitionComponent>(squadEntity);
            var squadState = SystemAPI.GetComponent<SquadStateComponent>(squadEntity);
            var formationComp = SystemAPI.GetComponent<FormationComponent>(squadEntity);

            // Get current formation gridPositions from squad definition
            ref BlobArray<int2> gridPositions = ref squadDef.formationLibrary.Value.formations[0].gridPositions;
            if (squadDef.formationLibrary.IsCreated)
            {
                ref var formations = ref squadDef.formationLibrary.Value.formations;
                FormationType currentFormation = formationComp.currentFormation;
                
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
            
            if (!SystemAPI.HasComponent<SquadFormationAnchorComponent>(squadEntity))
                continue;
            var anchorComp = SystemAPI.GetComponent<SquadFormationAnchorComponent>(squadEntity);
            float3 heroPos = anchorComp.position;

            // Obtener el componente de hold position si existe (needed by FormationPositionCalculator)
            SquadHoldPositionComponent? holdComponent = null;
            if (SystemAPI.HasComponent<SquadHoldPositionComponent>(squadEntity))
                holdComponent = SystemAPI.GetComponent<SquadHoldPositionComponent>(squadEntity);
            
            // Actualizar target positions basadas en grid slots
            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i].Value;
                if (!SystemAPI.HasComponent<UnitGridSlotComponent>(unit)) continue;
                
                var gridSlot = SystemAPI.GetComponent<UnitGridSlotComponent>(unit);
                
                // Use unified position calculator with current formation
                float3 targetPos = float3.zero;
                if (gridPositions.Length > 0 && i < gridPositions.Length)
                {
                    // Rotation already computed by SquadAnchorSystem (holdRotation or default)
                    quaternion formationRotation = anchorComp.rotation;

                    FormationPositionCalculator.CalculateDesiredPosition(
                        unit,
                        ref gridPositions,
                        i, // unitIndex
                        squadState,
                        holdComponent,
                        heroPos,
                        out int2 originalGridPos,
                        out float3 gridOffset,
                        out float3 worldPos,
                        true,
                        formationRotation);
                    targetPos = worldPos;
                }
                else
                {
                    // Fallback to grid slot offset if no formation data available
                    targetPos = heroPos + gridSlot.worldOffset;
                }
                
                // Actualizar target position si existe el componente
                if (SystemAPI.HasComponent<UnitTargetPositionComponent>(unit))
                {
                    var targetComp = SystemAPI.GetComponentRW<UnitTargetPositionComponent>(unit);
                    targetComp.ValueRW.position = targetPos;;
                }
            }
        }
    }
}
