using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Script que sincroniza la posición y rotación del GameObject visual
/// con la entidad ECS asociada. Se ejecuta en Update() para mantener
/// la visualización alineada con la lógica ECS.
/// </summary>
public class EntityVisualSync : MonoBehaviour
{
    [Header("Entity Sync Configuration")]
    public Entity entity;
    public EntityManager entityManager;
    
    [Header("Debug Info")]
    [SerializeField] private bool showDebugInfo = false;
    [SerializeField] private Vector3 lastEntityPosition;
    [SerializeField] private bool entityExists = false;
    
    private void Update()
    {
        SyncWithEntity();
    }
    
    /// <summary>
    /// Sincroniza la posición y rotación del GameObject con su entidad asociada.
    /// </summary>
    private void SyncWithEntity()
    {
        // Verificar si el EntityManager sigue siendo válido
        if (!IsEntityManagerValid())
        {
            entityExists = false;
            return;
        }
        
        if (entity == Entity.Null)
        {
            entityExists = false;
            return;
        }
        
        if (!entityManager.Exists(entity))
        {
            entityExists = false;
            // Si la entidad fue destruida, destruir también el GameObject visual
            DestroyVisual();
            return;
        }
        
        entityExists = true;
        
        // Sincronizar posición y rotación
        if (entityManager.HasComponent<LocalTransform>(entity))
        {
            var transform = entityManager.GetComponentData<LocalTransform>(entity);
            this.transform.position = transform.Position;
            this.transform.rotation = transform.Rotation;
            this.transform.localScale = Vector3.one * transform.Scale;
            
            if (showDebugInfo)
            {
                lastEntityPosition = transform.Position;
            }
        }
        
        // Sincronizar estado de vida (opcional: ocultar/mostrar el visual)
        if (entityManager.HasComponent<HeroLifeComponent>(entity))
        {
            var life = entityManager.GetComponentData<HeroLifeComponent>(entity);
            gameObject.SetActive(life.isAlive);
        }
    }
    
    /// <summary>
    /// Verifica si el EntityManager sigue siendo válido para usar.
    /// </summary>
    /// <returns>True si el EntityManager es válido, false si no</returns>
    private bool IsEntityManagerValid()
    {
        try
        {
            return entityManager.World != null && entityManager.World.IsCreated;
        }
        catch (System.ObjectDisposedException)
        {
            return false;
        }
    }
    
    /// <summary>
    /// Configura la sincronización con una entidad específica.
    /// </summary>
    /// <param name="targetEntity">Entidad a sincronizar</param>
    /// <param name="manager">EntityManager para acceder a los componentes</param>
    public void SetupSync(Entity targetEntity, EntityManager manager)
    {
        entity = targetEntity;
        entityManager = manager;
        
        Debug.Log($"[EntityVisualSync] Configurado para sincronizar entidad {targetEntity.Index}");
    }
    
    /// <summary>
    /// Destruye el GameObject visual cuando la entidad asociada es destruida.
    /// </summary>
    private void DestroyVisual()
    {
        Debug.Log($"[EntityVisualSync] Entidad {entity.Index} destruida, destruyendo visual");
        
        if (Application.isPlaying)
        {
            Destroy(gameObject);
        }
        else
        {
            DestroyImmediate(gameObject);
        }
    }
    
    /// <summary>
    /// Limpieza cuando el GameObject es destruido.
    /// </summary>
    private void OnDestroy()
    {
        // Opcional: remover el componente HeroVisualInstance de la entidad
        if (IsEntityManagerValid() && entityManager.Exists(entity) && 
            entityManager.HasComponent<HeroVisualInstance>(entity))
        {
            try
            {
                entityManager.RemoveComponent<HeroVisualInstance>(entity);
            }
            catch (System.ObjectDisposedException)
            {
                // EntityManager ya fue destruido, no hay nada que limpiar
            }
        }
    }
    
    // Debug visual en el Scene View
    private void OnDrawGizmosSelected()
    {
        if (showDebugInfo && entityExists)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2f);
        }
    }
}
