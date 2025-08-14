using UnityEngine;

/// <summary>
/// Componente que se asegura de que el HeroVisualManagementSystem esté activo
/// y conectado a los eventos de equipamiento. Se puede añadir a cualquier GameObject
/// en la escena de juego para garantizar la funcionalidad.
/// </summary>
public class HeroVisualEquipmentInitializer : MonoBehaviour
{
    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugLogs = true;

    void Start()
    {
        InitializeVisualEquipmentSystem();
    }

    /// <summary>
    /// Verifica que el sistema de gestión visual esté funcionando.
    /// </summary>
    private void InitializeVisualEquipmentSystem()
    {
        if (enableDebugLogs)
        {
            Debug.Log("[HeroVisualEquipmentInitializer] Initializing hero visual equipment system...");
        }

        // El HeroVisualManagementSystem se inicializa automáticamente como parte del ECS
        // Este componente solo sirve como punto de debug y verificación

        // Verificar que el nuevo sistema de inventario está disponible
        if (InventoryManager.IsInitialized)
        {
            if (enableDebugLogs)
            {
                Debug.Log("[HeroVisualEquipmentInitializer] InventoryManager is initialized and ready");
            }
        }
        else
        {
            if (enableDebugLogs)
            {
                Debug.Log("[HeroVisualEquipmentInitializer] InventoryManager not yet initialized - this is normal on startup");
            }
        }

        if (enableDebugLogs)
        {
            Debug.Log("[HeroVisualEquipmentInitializer] Hero visual equipment system initialization complete");
        }
    }

    /// <summary>
    /// Método de debug para forzar una actualización visual completa del héroe.
    /// </summary>
    [ContextMenu("Force Refresh Hero Visual")]
    public void ForceRefreshHeroVisual()
    {
        if (enableDebugLogs)
        {
            Debug.Log("[HeroVisualEquipmentInitializer] Force refresh triggered - this would need to be implemented in HeroVisualManagementSystem");
        }

        // En el futuro, podrías agregar un método público al HeroVisualManagementSystem
        // para forzar un refresh completo del equipamiento visual
    }
}
