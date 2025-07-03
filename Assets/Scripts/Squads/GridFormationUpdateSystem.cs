using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Sistema que mantiene sincronizados los componentes de grid con las posiciones de formación.
/// Simplificado para trabajar solo con grid-based formations.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(FormationSystem))]
public partial class GridFormationUpdateSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ownerLookup = GetComponentLookup<SquadOwnerComponent>(true);
        var transformLookup = GetComponentLookup<LocalTransform>(true);
        
        // Actualizar posiciones target cuando las unidades cambien de grid slot
        foreach (var (units, squadEntity) in SystemAPI
                    .Query<DynamicBuffer<SquadUnitElement>>()
                    .WithEntityAccess())
        {
            if (units.Length == 0) continue;
            
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
            
            // Obtener la posición del héroe como centro de referencia
            if (!ownerLookup.TryGetComponent(squadEntity, out var squadOwner)) continue;
            if (!transformLookup.TryGetComponent(squadOwner.hero, out var heroTransform)) continue;
            
            float3 heroPos = heroTransform.Position;
            
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
                    FormationPositionCalculator.CalculateDesiredPosition(
                        unit,
                        ref gridPositions,
                        heroPos, // GridFormationUpdateSystem siempre usa la posición actual del héroe
                        i,
                        out int2 originalGridPos,
                        out float3 gridOffset,
                        out float3 worldPos,
                        true);
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
                    targetComp.ValueRW.position = targetPos;
                }
            }
        }
    }
}
