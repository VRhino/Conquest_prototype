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
        if (!ValidateOperation(itemId, quantity))
        {   
            Debug.LogWarning($"Invalid operation for itemId '{itemId}' with quantity {quantity}");
            return false;
        }

        var existingItem = FindStackableItem(itemId);

        if (existingItem != null)
        {
            return StackExistingItem(existingItem, quantity, itemId);
        }
        else
        {
            return AddNewItem(itemId, quantity);
        }
    }

    /// <summary>
    /// Busca un item stackeable del mismo tipo
    /// </summary>
    private static InventoryItem FindStackableItem(string itemId)
    {
        return _currentHero.inventory.FirstOrDefault(item => 
            item.itemId == itemId && item.IsStackable);
    }

    /// <summary>
    /// Remueve un ítem del inventario.
    /// </summary>
    /// <param name="itemId">ID del ítem a remover</param>
    /// <param name="quantity">Cantidad a remover (por defecto 1)</param>
    /// <returns>True si se removió exitosamente, false si no existía o cantidad insuficiente</returns>
    public static bool RemoveItem(string itemId, int quantity = 1)
    {
        if (!ValidateOperation(itemId, quantity)) return false;

        var item = FindItemInInventory(itemId);
        if (item == null)
            return false;

        if (item.quantity < quantity)
            return false;

        return ProcessItemRemoval(item, quantity, itemId);
    }

    /// <summary>
    /// Equipa un ítem válido en el slot correspondiente.
    /// </summary>
    /// <param name="itemId">ID del ítem a equipar</param>
    /// <returns>True si se equipó exitosamente</returns>
    public static bool EquipItem(string itemId)
    {
        if (!ValidateService()) return false;
        
        var item = FindItemInInventory(itemId);
        if (item == null)
            return false;

        if (!InventoryUtils.IsEquippableType(item.itemType))
            return false;

        if (!CanEquipItemAdvanced(itemId))
            return false;

        return ProcessEquipment(itemId, item.itemType);
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
        EmitEquipmentEvents(currentItemId, isEquipping: false);
        
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

        var itemsToRemove = new List<InventoryItem>();
        bool repairsNeeded = false;

        foreach (var item in _currentHero.inventory)
        {
            if (ShouldRemoveItem(item))
            {
                itemsToRemove.Add(item);
                repairsNeeded = true;
            }
            else if (FixItemType(item))
            {
                repairsNeeded = true;
            }
        }

        RemoveInvalidItems(itemsToRemove);

        if (repairsNeeded)
            InventoryEvents.OnInventoryChanged?.Invoke();

        return !repairsNeeded;
    }

    /// <summary>
    /// Configura el límite de inventario para el héroe actual.
    /// </summary>
    public static void SetInventoryLimit(int newLimit)
    {
        InventoryLimit = newLimit;
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

    #region Private Methods

    #region Validation Methods
    
    private static bool ValidateService()
    {
        if (_currentHero == null || _itemDatabase == null)
            return false;
       
        return true;
    }

    private static bool ValidateOperation(string itemId, int quantity)
    {
        if (!ValidateService()) return false;
        
        if (!InventoryUtils.ValidateItemParameters(itemId, quantity))
            return false;
        
        if (!InventoryUtils.ItemExists(itemId))
            return false;
        
        return true;
    }

    #endregion

    #region Inventory Operations

    private static InventoryItem FindItemInInventory(string itemId)
    {
        try
        {
            return _currentHero.inventory.FirstOrDefault(i => i.itemId == itemId);
        }
        catch (Exception ex)
        {
            LogError($"Error finding item '{itemId}': {ex.Message}");
            return null;
        }
    }

    private static bool StackExistingItem(InventoryItem existingItem, int quantity, string itemId)
    {
        existingItem.quantity += quantity;
        
        EmitItemEvents(itemId, quantity, isAdding: true);
        return true;
    }

    private static bool AddNewItem(string itemId, int quantity)
    {
        if (_currentHero.inventory.Count >= InventoryLimit)
        {
            InventoryEvents.OnInventoryFull?.Invoke();
            return false;
        }

        // Crear nueva instancia del item
        var newItem = CreateItemInstance(itemId, quantity);
        if (newItem == null)
        {
            LogError($"Failed to create item instance: {itemId}");
            return false;
        }

        // Asignar slot automáticamente
        newItem.slotIndex = FindNextAvailableSlotIndex();
        
        _currentHero.inventory.Add(newItem);
        
        // Emitir evento adicional para equipment instances
        if (newItem.IsEquipment)
        {
            InventoryEvents.OnEquipmentInstanceGenerated?.Invoke(newItem.instanceId, itemId);
        }
        
        EmitItemEvents(itemId, quantity, isAdding: true);
        return true;
    }

    /// <summary>
    /// Crea una nueva instancia de item
    /// </summary>
    private static InventoryItem CreateItemInstance(string itemId, int quantity)
    {
        var itemData = InventoryUtils.GetItemData(itemId);
        if (itemData == null) return null;

        var item = new InventoryItem
        {
            itemId = itemId,
            itemType = itemData.itemType,
            quantity = quantity
        };

        // Si es equipment, generar stats únicos e instanceId
        if (itemData.IsEquipment)
        {
            item.instanceId = System.Guid.NewGuid().ToString();
            
            if (itemData.statGenerator != null)
            {
                item.GeneratedStats = itemData.statGenerator.GenerateStats();
            }
        }

        return item;
    }

    private static bool ProcessItemRemoval(InventoryItem item, int quantity, string itemId)
    {
        item.quantity -= quantity;
        
        if (item.quantity <= 0)
        {
            _currentHero.inventory.Remove(item);
        }
        EmitItemEvents(itemId, quantity, isAdding: false);
        return true;
    }

    /// <summary>
    /// Encuentra el próximo slot disponible en la grilla para colocar un nuevo ítem.
    /// </summary>
    private static int FindNextAvailableSlotIndex()
    {
        const int GRID_SIZE = 72; // 9x8 grilla
        
        // Crear un array para marcar slots ocupados
        bool[] occupiedSlots = new bool[GRID_SIZE];
        
        // Marcar slots ocupados por ítems existentes
        foreach (var item in _currentHero.inventory)
        {
            if (item.slotIndex >= 0 && item.slotIndex < GRID_SIZE)
            {
                occupiedSlots[item.slotIndex] = true;
            }
        }
        
        // Encontrar el primer slot libre
        for (int i = 0; i < GRID_SIZE; i++)
        {
            if (!occupiedSlots[i])
            {
                return i;
            }
        }
        
        // Si no hay slots libres, retornar -1 (esto no debería pasar si validamos inventoryLimit correctamente)
        return -1;
    }

    #endregion

    #region Equipment Operations

    private static bool CanEquipItemAdvanced(string itemId)
    {
        var itemData = _itemDatabase.GetItemDataById(itemId);
        if (itemData == null) return false;

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

    private static bool ProcessEquipment(string itemId, ItemType itemType)
    {
        // Desequipar ítem anterior si existe
        string previousItemId = GetEquippedItemInSlot(itemType);
        if (!string.IsNullOrEmpty(previousItemId))
        {
            UnequipItem(itemType);
        }

        // Equipar nuevo ítem
        SetEquippedItem(itemType, itemId);
        EmitEquipmentEvents(itemId, isEquipping: true);
        
        return true;
    }

    #endregion

    #region Event and Persistence Management

    private static void EmitItemEvents(string itemId, int quantity, bool isAdding)
    {
        if (isAdding)
        {
            InventoryEvents.OnItemAdded?.Invoke(itemId, quantity);
        }
        else
        {
            InventoryEvents.OnItemRemoved?.Invoke(itemId, quantity);
        }
        
        InventoryEvents.OnInventoryChanged?.Invoke();
        SavePlayerData();
    }

    private static void EmitEquipmentEvents(string itemId, bool isEquipping)
    {
        if (isEquipping)
        {
            InventoryEvents.OnItemEquipped?.Invoke(itemId);
        }
        else
        {
            InventoryEvents.OnItemUnequipped?.Invoke(itemId);
        }
        
        InventoryEvents.OnInventoryChanged?.Invoke();
        SavePlayerData();
    }

    /// <summary>
    /// Guarda automáticamente los datos del jugador cuando hay cambios en el inventario.
    /// </summary>
    private static void SavePlayerData()
    {
        try
        {
            var playerData = PlayerSessionService.CurrentPlayer;
            if (playerData != null)
            {
                SaveSystem.SavePlayer(playerData);
                LogInfo("Player data saved automatically after inventory change");
            }
            else
            {
                LogWarning("Cannot save player data: PlayerSessionService.CurrentPlayer is null");
            }
        }
        catch (System.Exception e)
        {
            LogError($"Failed to save player data: {e.Message}");
        }
    }

    #endregion

    #region Logging Utilities

    private static void LogInfo(string message)
    {
        Debug.Log($"[InventoryService] {message}");
    }

    private static void LogWarning(string message)
    {
        Debug.LogWarning($"[InventoryService] {message}");
    }

    private static void LogError(string message)
    {
        Debug.LogError($"[InventoryService] {message}");
    }

    #endregion

    #region Validation and Repair Methods

    private static bool ShouldRemoveItem(InventoryItem item)
    {
        if (!_itemDatabase.ItemExists(item.itemId))
        {
            LogWarning($"Removing invalid item '{item.itemId}' from inventory");
            return true;
        }

        if (item.quantity <= 0)
        {
            LogWarning($"Removing item '{item.itemId}' with invalid quantity {item.quantity}");
            return true;
        }

        return false;
    }

    private static bool FixItemType(InventoryItem item)
    {
        var correctType = _itemDatabase.GetItemType(item.itemId);
        if (item.itemType != correctType)
        {
            item.itemType = correctType;
            return true;
        }
        return false;
    }

    private static void RemoveInvalidItems(List<InventoryItem> itemsToRemove)
    {
        foreach (var item in itemsToRemove)
        {
            _currentHero.inventory.Remove(item);
        }
    }

    #endregion

    #region Consumable Usage

    /// <summary>
    /// Usa un item consumible del inventario
    /// </summary>
    /// <param name="itemId">ID del item a usar</param>
    /// <param name="quantity">Cantidad a usar (por defecto 1)</param>
    /// <returns>True si se usó exitosamente</returns>
    public static bool UseConsumableItem(string itemId, int quantity = 1)
    {
        if (_currentHero?.inventory == null) return false;

        var item = _currentHero.inventory.FirstOrDefault(i => 
            i.itemId == itemId && IsConsumableItem(i.itemId) && i.quantity >= quantity);

        if (item == null)
        {
            LogError($"Consumable item not found or insufficient quantity: {itemId}");
            return false;
        }

        var itemData = InventoryUtils.GetItemData(itemId);
        if (itemData?.effects == null || itemData.effects.Length == 0)
        {
            LogError($"Item {itemId} has no effects to execute");
            return false;
        }

        // Ejecutar efectos del item
        bool anyEffectExecuted = false;
        foreach (var effect in itemData.effects)
        {
            if (effect != null && effect.CanExecute(_currentHero))
            {
                effect.Execute(_currentHero);
                anyEffectExecuted = true;
                
                // Emitir evento para cada efecto ejecutado
                InventoryEvents.OnItemEffectExecuted?.Invoke(effect.GetType().Name, _currentHero);
            }
        }

        if (anyEffectExecuted)
        {
            // Reducir cantidad o remover item
            if (RemoveItem(itemId, quantity))
            {
                InventoryEvents.OnItemUsed?.Invoke(itemId, _currentHero);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Verifica si un item es consumible
    /// </summary>
    private static bool IsConsumableItem(string itemId)
    {
        var itemData = InventoryUtils.GetItemData(itemId);
        return itemData != null && itemData.IsConsumable;
    }

    #endregion

    #endregion
}