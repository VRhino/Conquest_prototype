using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Authoring component para marcar GameObjects como prefabs visuales.
/// Se usa para convertir prefabs de Synty en prefabs que pueden ser
/// referenciados desde entidades ECS sin convertirlos completamente.
/// </summary>
public class VisualPrefabAuthoring : MonoBehaviour
{
    [Header("Visual Prefab Configuration")]
    [Tooltip("Identificador único para este prefab visual")]
    public string prefabId = "HeroSynty";
    
    [Tooltip("Si es true, este prefab se registrará automáticamente en el VisualPrefabRegistry")]
    public bool autoRegister = true;
    
    [Header("Sync Configuration")]
    [Tooltip("Si es true, se agregará automáticamente el componente EntityVisualSync")]
    public bool addSyncComponent = true;
    
    private void Awake()
    {
        // Auto-registrar el prefab si está configurado para hacerlo
        if (autoRegister && Application.isPlaying)
        {
            VisualPrefabRegistry.Instance.RegisterPrefab(prefabId, gameObject);
        }
        
        // Agregar componente de sincronización si no existe
        if (addSyncComponent && GetComponent<EntityVisualSync>() == null)
        {
            gameObject.AddComponent<EntityVisualSync>();
        }
    }
}

/// <summary>
/// Baker que NO convierte el GameObject a entidad, pero permite referenciar
/// el prefab desde componentes ECS. Esto es clave para la solución híbrida.
/// </summary>
public class VisualPrefabBaker : Baker<VisualPrefabAuthoring>
{
    public override void Bake(VisualPrefabAuthoring authoring)
    {
        // IMPORTANTE: No convertir a entidad, solo crear una referencia
        // que pueda ser usada por HeroVisualReference
        
        // Opcionalmente, podemos crear una entidad "fantasma" que solo sirva
        // como referencia sin renderizar nada
        var entity = GetEntity(TransformUsageFlags.None);
        
        // Agregar un componente que identifique esto como un prefab visual
        AddComponent(entity, new VisualPrefabComponent
        {
            prefabId = authoring.prefabId
        });
        
        // No agregar otros componentes que causarían que se renderice como entidad
    }
}

/// <summary>
/// Componente que marca una entidad como referencia a un prefab visual.
/// </summary>
public struct VisualPrefabComponent : IComponentData
{
    public FixedString64Bytes prefabId;
}
