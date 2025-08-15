using UnityEngine;
using Data.Items;

/// <summary>
/// Efecto de item que otorga una pieza de equipment específica al héroe.
/// Crea una nueva instancia del equipment y la agrega al inventario.
/// </summary>
[CreateAssetMenu(fileName = "EquipmentGrantEffect", menuName = "Items/Effects/Equipment Grant", order = 2)]
public class EquipmentGrantEffect : ItemEffect
{
    [Header("Equipment Settings")]
    [SerializeField] private string targetItemId;
    [SerializeField] private bool allowDuplicates = true;
    
    [Header("Validation")]
    [SerializeField] private bool validateItemExists = true;
    [SerializeField] private bool requireInventorySpace = true;

    /// <summary>
    /// Ejecuta el efecto de otorgar equipment al héroe
    /// </summary>
    public override bool Execute(HeroData hero, int quantity = 1)
    {
        if (!CanExecute(hero))
        {
            LogError("Cannot execute EquipmentGrantEffect - validation failed");
            return false;
        }

        // Validar que el item existe en la base de datos
        var itemData = InventoryUtils.GetItemData(targetItemId);
        if (itemData == null)
        {
            LogError($"Item with ID '{targetItemId}' not found in database");
            return false;
        }

        // Verificar que es equipment
        if (!itemData.IsEquipment)
        {
            LogError($"Item '{targetItemId}' is not equipment - cannot grant with EquipmentGrantEffect");
            return false;
        }

        bool success = true;
        int successfulGrants = 0;

        // Crear múltiples instancias si la cantidad es mayor a 1
        for (int i = 0; i < quantity; i++)
        {
            // Crear nueva instancia de equipment
            var newEquipment = ItemInstanceService.CreateItem(targetItemId, 1);
            if (newEquipment == null)
            {
                LogError($"Failed to create equipment instance for '{targetItemId}'");
                success = false;
                continue;
            }

            // Intentar agregar al inventario
            bool added = false;
            added = InventoryManager.AddItem(targetItemId, 1);

            if (added)
            {
                successfulGrants++;
                LogInfo($"Successfully granted equipment '{itemData.name}' to hero '{hero.heroName}'");
                
                // Notificar evento
                InventoryEventService.TriggerItemAdded(newEquipment);
            }
            else
            {
                LogWarning($"Failed to add equipment '{targetItemId}' to inventory - no space available");
                success = false;
            }
        }

        // Consideramos éxito si al menos una instancia se agregó correctamente
        bool finalSuccess = successfulGrants > 0;
        
        if (finalSuccess)
        {
            LogInfo($"EquipmentGrantEffect completed: {successfulGrants}/{quantity} items granted successfully");
        }

        return finalSuccess;
    }

    /// <summary>
    /// Verifica si el efecto puede ser ejecutado
    /// </summary>
    public override bool CanExecute(HeroData hero)
    {
        if (!base.CanExecute(hero))
            return false;

        // Validar item ID
        if (string.IsNullOrEmpty(targetItemId))
        {
            LogError("Target item ID is empty");
            return false;
        }

        // Validar que el item existe (si está habilitado)
        if (validateItemExists)
        {
            var itemData = InventoryUtils.GetItemData(targetItemId);
            if (itemData == null)
            {
                LogError($"Target item '{targetItemId}' does not exist in database");
                return false;
            }

            if (!itemData.IsEquipment)
            {
                LogError($"Target item '{targetItemId}' is not equipment");
                return false;
            }
        }

        // Validar espacio en inventario (si está habilitado)
        if (requireInventorySpace && !InventoryStorageService.HasSpace())
        {
            LogWarning("No space available in inventory");
            return false;
        }

        // Validar duplicados (si no están permitidos)
        if (!allowDuplicates)
        {
            var existingItem = InventoryStorageService.FindItemById(targetItemId);
            if (existingItem != null)
            {
                LogWarning($"Equipment '{targetItemId}' already exists in inventory and duplicates are not allowed");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Texto de preview para mostrar en la UI
    /// </summary>
    public override string GetPreviewText(int quantity = 1)
    {
        var itemData = InventoryUtils.GetItemData(targetItemId);
        string itemName = itemData?.name ?? targetItemId;

        if (quantity == 1)
        {
            return $"Grants: {itemName}";
        }
        else
        {
            return $"Grants: {quantity}x {itemName}";
        }
    }

    /// <summary>
    /// Obtiene la prioridad de ejecución (equipment grants tienen alta prioridad)
    /// </summary>
    public override int GetExecutionPriority()
    {
        return 80; // Alta prioridad para que se ejecute antes que otros efectos
    }

    #region Public Configuration Methods

    /// <summary>
    /// Configura el item ID del equipment a otorgar
    /// </summary>
    /// <param name="itemId">ID del item en la base de datos</param>
    public void SetTargetItem(string itemId)
    {
        targetItemId = itemId;
    }

    /// <summary>
    /// Configura si se permiten duplicados del mismo equipment
    /// </summary>
    /// <param name="allowed">True si se permiten duplicados</param>
    public void SetAllowDuplicates(bool allowed)
    {
        allowDuplicates = allowed;
    }

    /// <summary>
    /// Obtiene el item ID configurado
    /// </summary>
    /// <returns>ID del item objetivo</returns>
    public string GetTargetItemId()
    {
        return targetItemId;
    }

    #endregion

    #region Validation Methods

    /// <summary>
    /// Valida la configuración del efecto en el editor
    /// </summary>
    public bool ValidateConfiguration()
    {
        if (string.IsNullOrEmpty(targetItemId))
        {
            Debug.LogError($"[{name}] Target item ID is not configured", this);
            return false;
        }

        if (validateItemExists)
        {
            var itemData = InventoryUtils.GetItemData(targetItemId);
            if (itemData == null)
            {
                Debug.LogError($"[{name}] Target item '{targetItemId}' not found in database", this);
                return false;
            }

            if (!itemData.IsEquipment)
            {
                Debug.LogError($"[{name}] Target item '{targetItemId}' is not equipment", this);
                return false;
            }
        }

        return true;
    }

    #endregion

    #region Logging

    private void LogInfo(string message)
    {
        Debug.Log($"[EquipmentGrantEffect] {message}");
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[EquipmentGrantEffect] {message}");
    }

    private void LogError(string message)
    {
        Debug.LogError($"[EquipmentGrantEffect] {message}");
    }

    #endregion

    #region Editor Validation

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate(); // Llamar al método padre si existe
        
        // Validar en tiempo de edición
        if (!string.IsNullOrEmpty(targetItemId) && validateItemExists)
        {
            var itemData = InventoryUtils.GetItemData(targetItemId);
            if (itemData == null)
            {
                Debug.LogWarning($"[{name}] Target item '{targetItemId}' not found in database", this);
            }
            else if (!itemData.IsEquipment)
            {
                Debug.LogWarning($"[{name}] Target item '{targetItemId}' is not equipment", this);
            }
        }
    }
#endif

    #endregion
}
