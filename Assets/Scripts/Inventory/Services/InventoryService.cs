using System;
using System.Collections.Generic;
using System.Linq;
using Data.Items;
using UnityEngine;

/// <summary>
/// Servicio centralizado para el manejo del inventario del héroe activo.
/// Maneja operaciones como agregar, remover, equipar ítems y emite eventos.
/// </summary>
public static class InventoryService
{
    private static HeroData _currentHero;
    private static ItemDatabase _itemDatabase;
    private static int _inventoryLimit = 20; // Límite configurable

    /// <summary>Límite máximo de slots del inventario.</summary>
    public static int InventoryLimit 
    { 
        get => _inventoryLimit; 
        set => _inventoryLimit = Mathf.Max(1, value); // Mínimo 1 slot
    }

    /// <summary>
    /// Inicializa el servicio con el héroe activo. Usa ItemDatabase.Instance automáticamente.
    /// </summary>
    public static void Initialize(HeroData hero)
    {
        _currentHero = hero;
        _itemDatabase = InventoryUtils.GetItemDatabase();
        
        if (_currentHero?.inventory == null)
        {
            Debug.LogWarning("[InventoryService] Hero inventory is null, initializing empty list");
            if (_currentHero != null)
                _currentHero.inventory = new List<InventoryItem>();
        }
    }

    /// <summary>
    /// Inicializa el servicio con el héroe activo y una base de datos específica.
    /// </summary>
    public static void Initialize(HeroData hero, ItemDatabase itemDatabase)
    {
        _currentHero = hero;
        _itemDatabase = itemDatabase;
        
        if (_currentHero?.inventory == null)
        {
            Debug.LogWarning("[InventoryService] Hero inventory is null, initializing empty list");
            if (_currentHero != null)
                _currentHero.inventory = new List<InventoryItem>();
        }
    }

    /// <summary>
    /// Agrega un ítem al inventario. Si es stackeable y ya existe, aumenta la cantidad.
    /// </summary>
    /// <param name="itemId">ID del ítem a agregar</param>
    /// <param name="quantity">Cantidad a agregar (por defecto 1)</param>
    /// <returns>True si se agregó exitosamente, false si no hay espacio</returns>
    public static bool AddItem(string itemId, int quantity = 1)
    {
        if (!ValidateService()) return false;
        
        // Validaciones de entrada
        if (!InventoryUtils.ValidateItemParameters(itemId, quantity))
        {
            return false;
        }
        
        if (!InventoryUtils.ItemExists(itemId))
        {
            Debug.LogWarning($"[InventoryService] Item '{itemId}' does not exist in database");
            return false;
        }

        var existingItem = _currentHero.inventory.FirstOrDefault(i => i.itemId == itemId);
        
        // Si el ítem ya existe y es stackeable, aumentar cantidad
        if (existingItem != null && InventoryUtils.IsStackable(itemId))
        {
            existingItem.quantity += quantity;
            Debug.Log($"[InventoryService] Stacked {quantity} of '{itemId}'. New total: {existingItem.quantity}");
        }
        else
        {
            // Verificar límite de slots solo para ítems no stackeables o nuevos
            if (_currentHero.inventory.Count >= InventoryLimit)
            {
                Debug.LogWarning($"[InventoryService] Inventory full! Cannot add '{itemId}'. Limit: {InventoryLimit}");
                InventoryEvents.OnInventoryFull?.Invoke();
                return false;
            }

            // Crear nuevo ítem
            var itemData = InventoryUtils.GetItemData(itemId);
            var newItem = new InventoryItem
            {
                itemId = itemId,
                itemType = itemData?.itemType ?? ItemType.None,
                quantity = quantity
            };
            _currentHero.inventory.Add(newItem);
            Debug.Log($"[InventoryService] Added new item '{itemId}' x{quantity} to inventory");
        }

        // Emitir eventos
        InventoryEvents.OnItemAdded?.Invoke(itemId, quantity);
        InventoryEvents.OnInventoryChanged?.Invoke();
        
        return true;
    }

    /// <summary>
    /// Remueve un ítem del inventario.
    /// </summary>
    /// <param name="itemId">ID del ítem a remover</param>
    /// <param name="quantity">Cantidad a remover (por defecto 1)</param>
    /// <returns>True si se removió exitosamente, false si no existía o cantidad insuficiente</returns>
    public static bool RemoveItem(string itemId, int quantity = 1)
    {
        if (!ValidateService()) return false;
        
        // Validaciones de entrada
        if (!InventoryUtils.ValidateItemParameters(itemId, quantity))
        {
            return false;
        }

        var item = _currentHero.inventory.FirstOrDefault(i => i.itemId == itemId);
        if (item == null)
        {
            Debug.LogWarning($"[InventoryService] Cannot remove item '{itemId}': not found in inventory");
            return false;
        }

        // Verificar que hay suficiente cantidad
        if (item.quantity < quantity)
        {
            Debug.LogWarning($"[InventoryService] Cannot remove {quantity} of '{itemId}': only {item.quantity} available");
            return false;
        }

        item.quantity -= quantity;
        
        // Si la cantidad llega a 0 o menos, remover completamente
        if (item.quantity <= 0)
        {
            _currentHero.inventory.Remove(item);
            Debug.Log($"[InventoryService] Completely removed '{itemId}' from inventory");
        }
        else
        {
            Debug.Log($"[InventoryService] Removed {quantity} of '{itemId}'. Remaining: {item.quantity}");
        }

        // Emitir eventos
        InventoryEvents.OnItemRemoved?.Invoke(itemId, quantity);
        InventoryEvents.OnInventoryChanged?.Invoke();
        
        return true;
    }

    /// <summary>
    /// Equipa un ítem válido en el slot correspondiente.
    /// </summary>
    /// <param name="itemId">ID del ítem a equipar</param>
    /// <returns>True si se equipó exitosamente</returns>
    public static bool EquipItem(string itemId)
    {
        if (!ValidateService()) return false;
        
        var item = _currentHero.inventory.FirstOrDefault(i => i.itemId == itemId);
        if (item == null)
        {
            Debug.LogWarning($"[InventoryService] Cannot equip item '{itemId}': not found in inventory");
            return false;
        }

        var itemType = item.itemType;
        
        // Verificar que sea un ítem equipable
        if (!InventoryUtils.IsEquippableType(itemType))
        {
            Debug.LogWarning($"[InventoryService] Cannot equip item '{itemId}': type '{itemType}' is not equippable");
            return false;
        }

        // Validaciones adicionales de equipamiento
        if (!CanEquipItemAdvanced(itemId))
        {
            Debug.LogWarning($"[InventoryService] Cannot equip item '{itemId}': advanced validation failed");
            return false;
        }

        // Desequipar ítem anterior si existe
        string previousItemId = GetEquippedItemInSlot(itemType);
        if (!string.IsNullOrEmpty(previousItemId))
        {
            UnequipItem(itemType);
        }

        // Equipar nuevo ítem
        SetEquippedItem(itemType, itemId);

        // Emitir eventos
        InventoryEvents.OnItemEquipped?.Invoke(itemId);
        InventoryEvents.OnInventoryChanged?.Invoke();
        
        return true;
    }

    /// <summary>
    /// Desequipa un ítem del slot especificado.
    /// </summary>
    public static bool UnequipItem(ItemType slot)
    {
        if (!ValidateService()) return false;
        
        string currentItemId = GetEquippedItemInSlot(slot);
        if (string.IsNullOrEmpty(currentItemId)) return false;

        SetEquippedItem(slot, string.Empty);

        // Emitir eventos
        InventoryEvents.OnItemUnequipped?.Invoke(currentItemId);
        InventoryEvents.OnInventoryChanged?.Invoke();
        
        return true;
    }

    /// <summary>
    /// Ordena el inventario por tipo y luego por ID.
    /// </summary>
    public static void SortByType()
    {
        if (!ValidateService()) return;

        _currentHero.inventory = _currentHero.inventory
            .OrderBy(i => i.itemType)
            .ThenBy(i => i.itemId)
            .ToList();

        InventoryEvents.OnInventorySorted?.Invoke();
        InventoryEvents.OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// Obtiene una copia de todos los ítems del inventario.
    /// </summary>
    public static List<InventoryItem> GetAllItems()
    {
        if (!ValidateService()) return new List<InventoryItem>();
        return new List<InventoryItem>(_currentHero.inventory);
    }

    /// <summary>
    /// Obtiene la cantidad de un ítem específico en el inventario.
    /// </summary>
    public static int GetItemQuantity(string itemId)
    {
        if (!ValidateService()) return 0;
        var item = _currentHero.inventory.FirstOrDefault(i => i.itemId == itemId);
        return item?.quantity ?? 0;
    }

    /// <summary>
    /// Verifica si hay espacio en el inventario para más ítems.
    /// </summary>
    public static bool HasSpace()
    {
        if (!ValidateService()) return false;
        return _currentHero.inventory.Count < InventoryLimit;
    }

    /// <summary>
    /// Obtiene el número de slots ocupados actualmente.
    /// </summary>
    public static int GetOccupiedSlots()
    {
        if (!ValidateService()) return 0;
        return _currentHero.inventory.Count;
    }

    /// <summary>
    /// Verifica si un ítem específico existe en el inventario.
    /// </summary>
    public static bool HasItem(string itemId)
    {
        if (!ValidateService()) return false;
        return _currentHero.inventory.Any(i => i.itemId == itemId);
    }

    /// <summary>
    /// Obtiene todos los ítems de un tipo específico.
    /// </summary>
    public static List<InventoryItem> GetItemsByType(ItemType itemType)
    {
        if (!ValidateService()) return new List<InventoryItem>();
        return _currentHero.inventory.Where(i => i.itemType == itemType).ToList();
    }

    /// <summary>
    /// Fuerza la emisión del evento OnInventoryChanged. Útil para refresh de UI.
    /// </summary>
    public static void RefreshInventoryUI()
    {
        InventoryEvents.OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// Valida y repara inconsistencias en el inventario.
    /// </summary>
    public static bool ValidateAndRepairInventory()
    {
        if (!ValidateService()) return false;

        bool repairsNeeded = false;
        var itemsToRemove = new List<InventoryItem>();

        foreach (var item in _currentHero.inventory)
        {
            // Verificar que el ítem existe en la base de datos
            if (!_itemDatabase.ItemExists(item.itemId))
            {
                Debug.LogWarning($"[InventoryService] Removing invalid item '{item.itemId}' from inventory");
                itemsToRemove.Add(item);
                repairsNeeded = true;
                continue;
            }

            // Verificar que la cantidad sea válida
            if (item.quantity <= 0)
            {
                Debug.LogWarning($"[InventoryService] Removing item '{item.itemId}' with invalid quantity {item.quantity}");
                itemsToRemove.Add(item);
                repairsNeeded = true;
                continue;
            }

            // Sincronizar el tipo del ítem con la base de datos
            var correctType = _itemDatabase.GetItemType(item.itemId);
            if (item.itemType != correctType)
            {
                Debug.LogWarning($"[InventoryService] Correcting item type for '{item.itemId}': {item.itemType} -> {correctType}");
                item.itemType = correctType;
                repairsNeeded = true;
            }
        }

        // Remover ítems inválidos
        foreach (var item in itemsToRemove)
        {
            _currentHero.inventory.Remove(item);
        }

        if (repairsNeeded)
        {
            InventoryEvents.OnInventoryChanged?.Invoke();
            Debug.Log($"[InventoryService] Inventory validation complete. {itemsToRemove.Count} invalid items removed.");
        }

        return !repairsNeeded; // Return true si no se necesitaron reparaciones
    }

    /// <summary>
    /// Configura el límite de inventario para el héroe actual.
    /// </summary>
    public static void SetInventoryLimit(int newLimit)
    {
        InventoryLimit = newLimit;
        Debug.Log($"[InventoryService] Inventory limit set to {InventoryLimit}");
    }

    /// <summary>
    /// Obtiene estadísticas del inventario actual.
    /// </summary>
    public static InventoryStats GetInventoryStats()
    {
        if (!ValidateService()) return new InventoryStats();

        var stats = new InventoryStats
        {
            TotalItems = _currentHero.inventory.Count,
            TotalQuantity = _currentHero.inventory.Sum(i => i.quantity),
            InventoryLimit = InventoryLimit,
            UsagePercentage = (float)_currentHero.inventory.Count / InventoryLimit * 100f
        };

        // Contar por tipos usando InventoryUtils
        var typeCounts = InventoryUtils.CountItemsByType(_currentHero.inventory);
        
        stats.WeaponCount = typeCounts.GetValueOrDefault(ItemType.Weapon, 0);
        stats.HelmetCount = typeCounts.GetValueOrDefault(ItemType.Helmet, 0);
        stats.TorsoCount = typeCounts.GetValueOrDefault(ItemType.Torso, 0);
        stats.GlovesCount = typeCounts.GetValueOrDefault(ItemType.Gloves, 0);
        stats.PantsCount = typeCounts.GetValueOrDefault(ItemType.Pants, 0);
        stats.BootsCount = typeCounts.GetValueOrDefault(ItemType.Boots, 0);
        stats.ConsumableCount = typeCounts.GetValueOrDefault(ItemType.Consumable, 0);
        stats.VisualCount = typeCounts.GetValueOrDefault(ItemType.Visual, 0);

        return stats;
    }

    /// <summary>
    /// Imprime un resumen detallado del inventario para debugging.
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public static void DebugPrintInventory()
    {
        if (!ValidateService())
        {
            Debug.Log("[InventoryService] Service not initialized for debug print");
            return;
        }

        var stats = GetInventoryStats();
        Debug.Log($"=== INVENTORY DEBUG ({_currentHero.heroName}) ===");
        Debug.Log($"Slots used: {stats.TotalItems}/{stats.InventoryLimit} ({stats.UsagePercentage:F1}%)");
        Debug.Log($"Total quantity: {stats.TotalQuantity}");
        Debug.Log($"Items by type: W:{stats.WeaponCount} H:{stats.HelmetCount} T:{stats.TorsoCount} G:{stats.GlovesCount} P:{stats.PantsCount} C:{stats.ConsumableCount} V:{stats.VisualCount}");
        
        Debug.Log("=== ITEMS ===");
        foreach (var item in _currentHero.inventory)
        {
            var itemName = _itemDatabase.GetItemName(item.itemId);
            Debug.Log($"- {itemName} ({item.itemId}) | Type: {item.itemType} | Qty: {item.quantity}");
        }
        Debug.Log("=== END INVENTORY ===");
    }

    #region Private Methods

    private static bool ValidateService()
    {
        if (_currentHero == null)
        {
            Debug.LogError("[InventoryService] Service not initialized: hero is null");
            return false;
        }
        if (_itemDatabase == null)
        {
            Debug.LogError("[InventoryService] Service not initialized: item database is null");
            return false;
        }
        return true;
    }

    private static bool CanEquipItemAdvanced(string itemId)
    {
        var itemData = _itemDatabase.GetItemDataById(itemId);
        if (itemData == null) return false;

        // Verificar que sea un tipo equipable
        if (!InventoryUtils.IsEquippableType(itemData.itemType)) return false;

        // Aquí se pueden agregar validaciones futuras:
        // - Nivel requerido del héroe
        // - Clase de héroe compatible
        // - Atributos mínimos requeridos
        // - Restricciones especiales del ítem

        return true;
    }

    private static string GetEquippedItemInSlot(ItemType slot)
    {
        if (_currentHero?.equipment == null) return string.Empty;

        return InventoryUtils.GetEquippedItemId(_currentHero.equipment, slot);
    }

    private static void SetEquippedItem(ItemType slot, string itemId)
    {
        if (_currentHero?.equipment == null) return;

        InventoryUtils.SetEquippedItemId(_currentHero.equipment, slot, itemId);
    }

    #endregion
}