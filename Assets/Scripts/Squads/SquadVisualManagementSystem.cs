using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using ConquestTactics.Visual;

/// <summary>
/// Sistema que gestiona la instanciación y sincronización de los GameObjects visuales
/// para las unidades de los squads. Los squads son entidades lógicas sin visual propio,
/// solo las unidades individuales tienen representación visual.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(SquadSpawningSystem))]
public partial class SquadVisualManagementSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        
        // Solo crear visuales para unidades individuales
        // Los squads son entidades lógicas sin visual propio
        CreateUnitVisuals(ecb);
        
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
    
    /// <summary>
    /// Crea visuales para unidades que no tienen visual todavía.
    /// Los squads no tienen visuales propios, solo las unidades individuales.
    /// </summary>
    private void CreateUnitVisuals(EntityCommandBuffer ecb)
    {
        foreach (var (unitVisualRef, transform, entity) in
                 SystemAPI.Query<RefRO<UnitVisualReference>, 
                                 RefRO<LocalTransform>>()
                        .WithNone<UnitVisualInstance>()
                        .WithEntityAccess())
        {
            CreateVisualForUnit(entity, unitVisualRef.ValueRO, transform.ValueRO, ecb);
        }
    }
    
    /// <summary>
    /// Crea un GameObject visual para la unidad especificada.
    /// </summary>
    /// <param name="unitEntity">Entidad de la unidad</param>
    /// <param name="visualRef">Referencia al prefab visual</param>
    /// <param name="transform">Transform inicial de la unidad</param>
    /// <param name="ecb">EntityCommandBuffer para agregar componentes</param>
    private void CreateVisualForUnit(Entity unitEntity, UnitVisualReference visualRef, 
        LocalTransform transform, EntityCommandBuffer ecb)
    {
        // Buscar el squad padre para determinar el tipo
        Entity parentSquad = FindParentSquad(unitEntity);
        SquadType squadType = SquadType.Squires; // Default
        
        if (parentSquad != Entity.Null && EntityManager.HasComponent<SquadDataReference>(parentSquad))
        {
            var squadDataRef = EntityManager.GetComponentData<SquadDataReference>(parentSquad);
            if (EntityManager.Exists(squadDataRef.dataEntity) && 
                EntityManager.HasComponent<SquadDataComponent>(squadDataRef.dataEntity))
            {
                var squadData = EntityManager.GetComponentData<SquadDataComponent>(squadDataRef.dataEntity);
                squadType = squadData.squadType;
            }
        }
        
        // Buscar el prefab visual
        GameObject visualPrefab = FindUnitVisualPrefab(visualRef.visualPrefabName.ToString(), squadType);
        
        if (visualPrefab == null)
        {
            Debug.LogWarning($"[SquadVisualManagementSystem] Prefab visual de unidad no encontrado: {visualRef.visualPrefabName}");
            return;
        }
        
        // Instanciar el GameObject visual de la unidad
        GameObject visualInstance = Object.Instantiate(visualPrefab);
        visualInstance.transform.position = transform.Position;
        visualInstance.transform.rotation = transform.Rotation;
        visualInstance.name = $"Unit_{unitEntity.Index}_Visual";
        
        // Configurar sincronización con la entidad de la unidad
        var syncScript = visualInstance.GetComponent<EntityVisualSync>();
        if (syncScript == null)
        {
            syncScript = visualInstance.AddComponent<EntityVisualSync>();
        }
        syncScript.SetHeroEntity(unitEntity);
        
        // Marcar la unidad como teniendo visual
        ecb.AddComponent(unitEntity, new UnitVisualInstance
        {
            visualInstanceId = visualInstance.GetInstanceID(),
            parentSquad = parentSquad
        });
        
        // Visual de unidad creado
    }
    
    /// <summary>
    /// Busca el squad padre de una unidad.
    /// </summary>
    /// <param name="unitEntity">Entidad de la unidad</param>
    /// <returns>Entidad del squad padre o Entity.Null</returns>
    private Entity FindParentSquad(Entity unitEntity)
    {
        // Buscar en todos los squads para encontrar el que contiene esta unidad
        foreach (var (units, squadEntity) in SystemAPI.Query<DynamicBuffer<SquadUnitElement>>().WithEntityAccess())
        {
            for (int i = 0; i < units.Length; i++)
            {
                if (units[i].Value == unitEntity)
                {
                    return squadEntity;
                }
            }
        }
        
        return Entity.Null;
    }
    
    /// <summary>
    /// Busca el prefab visual de la unidad usando el VisualPrefabRegistry.
    /// </summary>
    /// <param name="prefabName">Nombre del prefab</param>
    /// <param name="squadType">Tipo de squad para determinar el tipo de unidad</param>
    /// <returns>GameObject del prefab visual o null</returns>
    private GameObject FindUnitVisualPrefab(string prefabName, SquadType squadType)
    {
        var registry = VisualPrefabRegistry.Instance;
        
        // Intentar por nombre específico primero
        if (!string.IsNullOrEmpty(prefabName))
        {
            GameObject prefab = registry.GetPrefab(prefabName);
            if (prefab != null) return prefab;
        }
        
        // Fallback al tipo de squad
        return registry.GetDefaultUnitPrefab(squadType);
    }
}