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
                float3 targetPos = heroPos + gridSlot.worldOffset;
                
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
