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
        // Mantener consistencia entre UnitGridSlotComponent y UnitFormationSlotComponent
        foreach (var (gridSlot, formationSlot, entity) in SystemAPI
                    .Query<RefRO<UnitGridSlotComponent>,
                           RefRW<UnitFormationSlotComponent>>()
                    .WithEntityAccess())
        {
            // Convertir posición de grid a offset para compatibilidad con sistemas existentes
            float3 worldOffset = FormationGridSystem.GridToRelativeWorld(gridSlot.ValueRO.gridPosition);
            
            // Solo actualizar si el offset cambió
            if (!worldOffset.Equals(formationSlot.ValueRO.relativeOffset))
            {
                formationSlot.ValueRW.relativeOffset = worldOffset;
            }
        }
        
        // Actualizar posiciones target cuando las unidades cambien de grid slot
        foreach (var (units, squadEntity) in SystemAPI
                    .Query<DynamicBuffer<SquadUnitElement>>()
                    .WithEntityAccess())
        {
            if (units.Length == 0) continue;
            
            // La primera unidad es el centro de referencia del squad
            Entity leader = units[0].Value;
            if (!SystemAPI.HasComponent<LocalTransform>(leader)) continue;
            
            float3 leaderPos = SystemAPI.GetComponent<LocalTransform>(leader).Position;
            
            // Actualizar target positions basadas en grid slots
            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i].Value;
                if (!SystemAPI.HasComponent<UnitGridSlotComponent>(unit)) continue;
                
                var gridSlot = SystemAPI.GetComponent<UnitGridSlotComponent>(unit);
                float3 targetPos = leaderPos + gridSlot.worldOffset;
                
                // Ajustar altura del terreno
                if (UnityEngine.Terrain.activeTerrain != null)
                {
                    float terrainHeight = UnityEngine.Terrain.activeTerrain.SampleHeight(new UnityEngine.Vector3(targetPos.x, 0, targetPos.z));
                    terrainHeight += UnityEngine.Terrain.activeTerrain.GetPosition().y;
                    targetPos.y = terrainHeight;
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
