using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using ConquestTactics.Visual;

/// <summary>
/// Sistema que gestiona la instanciación y sincronización de los GameObjects visuales
/// para las entidades del héroe. Se ejecuta después del HeroSpawnSystem para crear
/// los visuales cuando se instancia una nueva entidad de héroe.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(HeroSpawnSystem))]
public partial class HeroVisualManagementSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        
        // Crear visuales para entidades recién spawneadas
        foreach (var (spawn, visualRef, transform, entity) in
                 SystemAPI.Query<RefRO<HeroSpawnComponent>, 
                                 RefRO<HeroVisualReference>, 
                                 RefRO<LocalTransform>>()
                        .WithAll<IsLocalPlayer>()
                        .WithNone<HeroVisualInstance>()
                        .WithEntityAccess())
        {
            // Asegurar que la entidad esté completamente spawneada
            if (!spawn.ValueRO.hasSpawned)
            {
                Debug.Log($"[HeroVisualManagementSystem] Entity {entity} not yet spawned, skipping visual creation");
                continue;
            }
                
            Debug.Log($"[HeroVisualManagementSystem] Creating visual for entity {entity} at position {transform.ValueRO.Position}");
            CreateVisualForEntity(entity, visualRef.ValueRO, transform.ValueRO, ecb);
        }
        
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
    
    /// <summary>
    /// Crea un GameObject visual para la entidad especificada.
    /// </summary>
    /// <param name="entity">Entidad para la que crear el visual</param>
    /// <param name="visualRef">Referencia al prefab visual</param>
    /// <param name="transform">Transform inicial de la entidad</param>
    /// <param name="ecb">EntityCommandBuffer para agregar componentes</param>
    private void CreateVisualForEntity(Entity entity, HeroVisualReference visualRef, 
        LocalTransform transform, EntityCommandBuffer ecb)
    {
        // Intentar obtener el prefab visual desde el EntityManager
        if (!EntityManager.Exists(visualRef.visualPrefab))
        {
            Debug.LogWarning($"[HeroVisualManagementSystem] Prefab visual no encontrado para entidad {entity.Index}");
            return;
        }
        
        // Para esta implementación híbrida, necesitamos una referencia al prefab GameObject
        // Como workaround, buscaremos el prefab por nombre o usaremos un sistema de registro
        GameObject visualPrefab = FindVisualPrefabGameObject("HeroSynty");
        
        if (visualPrefab == null)
        {
            Debug.LogWarning($"[HeroVisualManagementSystem] GameObject del prefab visual no encontrado");
            return;
        }
        
        // Instanciar el GameObject visual
        GameObject visualInstance = Object.Instantiate(visualPrefab);
        visualInstance.transform.position = transform.Position;
        visualInstance.transform.rotation = transform.Rotation;
        visualInstance.transform.localScale = Vector3.one * transform.Scale;
        
        Debug.Log($"[HeroVisualManagementSystem] Visual spawned at position: {transform.Position} for entity {entity}");
        
        // Configurar el script de sincronización
        EntityVisualSync syncScript = visualInstance.GetComponent<EntityVisualSync>();
        if (syncScript == null)
        {
            syncScript = visualInstance.AddComponent<EntityVisualSync>();
        }
        
        syncScript.SetHeroEntity(entity);
        
        Debug.Log($"[HeroVisualManagementSystem] EntityVisualSync configured for entity {entity}");
        
        // Marcar la entidad como teniendo un visual instanciado
        ecb.AddComponent(entity, new HeroVisualInstance
        {
            visualInstanceId = visualInstance.GetInstanceID()
        });
        
        // Visual created successfully
    }
    
    /// <summary>
    /// Busca el prefab GameObject visual usando el VisualPrefabRegistry.
    /// </summary>
    /// <param name="prefabName">Nombre del prefab a buscar</param>
    /// <returns>GameObject del prefab visual o null si no se encuentra</returns>
    private GameObject FindVisualPrefabGameObject(string prefabName)
    {
        // Usar el registro de prefabs visuales
        VisualPrefabRegistry registry = VisualPrefabRegistry.Instance;
        GameObject prefab = registry.GetPrefab(prefabName);
        
        if (prefab == null)
        {
            // Fallback: usar el prefab por defecto del héroe
            prefab = registry.GetDefaultHeroPrefab();
        }
        
        return prefab;
    }
}
