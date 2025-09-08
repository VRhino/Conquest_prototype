using System;
using System.Collections.Generic;
using System.Linq;
using BattleDrakeStudios.ModularCharacters;
using Data.Items;
using UnityEngine;

/// <summary>
/// Servicio especializado en operaciones básicas de almacenamiento del inventario.
/// Maneja agregar, remover, mover y buscar items sin lógica de negocio específica.
/// </summary>
public static class InventoryStorageService
{
    private static HeroData _currentHero;
    private static int _inventoryLimit = 72;

    /// <summary>Límite máximo de slots del inventario.</summary>
    public static int InventoryLimit 
    { 
        get => _inventoryLimit; 
        set => _inventoryLimit = Mathf.Max(1, value);
    }

    /// <summary>
    /// Inicializa el servicio con el héroe activo.
    /// </summary>
    public static void Initialize(HeroData hero)
    {
        _currentHero = hero ?? throw new ArgumentNullException(nameof(hero));
        
        if (_currentHero.inventory == null)
        {
            _currentHero.inventory = new List<InventoryItem>();
            LogInfo("Initialized empty inventory for hero");
        }
        
        ValidateInventoryIntegrity();
        LogInfo($"Storage service initialized for hero: {_currentHero.heroName}");
    }

    #region Add Operations

    /// <summary>
    /// Agrega un item existente al inventario en el primer slot disponible.
    /// </summary>
    public static bool AddExistingItem(InventoryItem item)
    {
        if (!ValidateAddOperation(item)) return false;

        // Para stackables, buscar si ya existe uno del mismo tipo
        if (item.IsStackable)
        {
            var existingStackable = FindStackableItem(item.itemId);
            if (existingStackable != null)
            {
                if (ItemInstanceService.StackItems(existingStackable, item))
                {
                    LogInfo($"Stacked {item.quantity} {item.itemId} with existing stack");
                    return true;
                }
            }
        }

        // Buscar slot disponible
        int availableSlot = InventoryUtils.FindNextAvailableSlotIndex(_currentHero.inventory, _inventoryLimit);
        if (availableSlot == -1)
        {
            LogWarning("No available slots in inventory");
            return false;
        }

        item.slotIndex = availableSlot;
        _currentHero.inventory.Add(item);
        
        LogInfo($"Added {item.itemId} to slot {availableSlot}");
        return true;
    }

    /// <summary>
    /// Agrega un item existente al inventario en un slot específico.
    /// </summary>
    public static bool AddExistingItemAtSlot(InventoryItem item, int targetSlotIndex)
    {
        if (!ValidateAddOperation(item)) return false;
        if (!IsValidSlotIndex(targetSlotIndex)) return false;

        if (IsSlotOccupied(targetSlotIndex))
        {
            LogWarning($"Slot {targetSlotIndex} is already occupied");
            return false;
        }

        item.slotIndex = targetSlotIndex;
        _currentHero.inventory.Add(item);
        
        LogInfo($"Added {item.itemId} to specific slot {targetSlotIndex}");
        return true;
    }

    #endregion

    #region Remove Operations

    /// <summary>
    /// Remueve un item específico del inventario por referencia.
    /// </summary>
    public static bool RemoveSpecificItem(InventoryItem item)
    {
        if (!ValidateRemoveOperation(item)) return false;

        bool removed = _currentHero.inventory.Remove(item);
        if (removed)
        {
            LogInfo($"Removed specific item {item.itemId} from slot {item.slotIndex}");
        }
        else
        {
            LogWarning($"Failed to remove item {item.itemId} - not found in inventory");
        }
        
        return removed;
    }

    /// <summary>
    /// Remueve una cantidad específica de un item stackable.
    /// </summary>
    public static bool RemoveItemQuantity(string itemId, int quantity)
    {
        if (!ValidateOperation(itemId, quantity)) return false;

        var stackableItem = FindStackableItem(itemId);
        if (stackableItem == null)
        {
            LogWarning($"Stackable item {itemId} not found");
            return false;
        }

        if (stackableItem.quantity < quantity)
        {
            LogWarning($"Insufficient quantity. Required: {quantity}, Available: {stackableItem.quantity}");
            return false;
        }

        stackableItem.quantity -= quantity;
        
        // Si la cantidad llega a 0, remover el item completamente
        if (stackableItem.quantity <= 0)
        {
            _currentHero.inventory.Remove(stackableItem);
            LogInfo($"Removed empty stack of {itemId}");
        }
        else
        {
            LogInfo($"Reduced {itemId} quantity by {quantity}. Remaining: {stackableItem.quantity}");
        }

        return true;
    }

    #endregion

    #region Move and Swap Operations

    /// <summary>
    /// Mueve un item a un slot específico.
    /// </summary>
    public static bool MoveItemToSlot(InventoryItem item, int targetSlotIndex)
    {
        if (!ValidateRemoveOperation(item)) return false;
        if (!IsValidSlotIndex(targetSlotIndex)) return false;

        if (IsSlotOccupied(targetSlotIndex))
        {
            LogWarning($"Cannot move item to occupied slot {targetSlotIndex}");
            return false;
        }

        item.slotIndex = targetSlotIndex;
        LogInfo($"Moved {item.itemId} to slot {targetSlotIndex}");
        return true;
    }

    /// <summary>
    /// Intercambia las posiciones de dos items.
    /// </summary>
    public static bool SwapItemSlots(InventoryItem item1, InventoryItem item2)
    {
        if (!ValidateRemoveOperation(item1) || !ValidateRemoveOperation(item2)) return false;

        int tempSlot = item1.slotIndex;
        item1.slotIndex = item2.slotIndex;
        item2.slotIndex = tempSlot;

        LogInfo($"Swapped slots: {item1.itemId} <-> {item2.itemId}");
        return true;
    }

    #endregion

    #region Query Operations

    /// <summary>
    /// Busca un item por su instanceId único.
    /// </summary>
    public static InventoryItem FindItemByInstanceId(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId)) return null;
        
        return _currentHero.inventory.FirstOrDefault(item => item.instanceId == instanceId);
    }

    /// <summary>
    /// Busca el primer item con el ID especificado.
    /// </summary>
    public static InventoryItem FindItemById(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        
        return _currentHero.inventory.FirstOrDefault(item => item.itemId == itemId);
    }

    /// <summary>
    /// Obtiene todos los items de un tipo específico.
    /// </summary>
    public static List<InventoryItem> GetItemsOfType(ItemType itemType)
    {
        return _currentHero.inventory.Where(item => item.itemType == itemType).ToList();
    }

    public static List<InventoryItem> GetItemsByTypeAndCategory(InventoryItem item)
    {
        ItemData protoItem = InventoryUtils.GetItemData(item.itemId);
        if (protoItem == null)
        {
            Debug.LogWarning($"ItemData not found for item: {item.itemId}");
            return new List<InventoryItem>();
        }
        return GetItemsByTypeAndCategory(protoItem.itemType, protoItem.itemCategory);
    }

    public static List<InventoryItem> GetItemsByTypeAndCategory(ItemType itemType, ItemCategory itemCategory)
    {

        if (itemType == ItemType.Weapon)
        {
            return _currentHero.inventory
                .Where(i => i.itemType == ItemType.Weapon)
                .ToList();
        }

        if (itemType == ItemType.Armor)
        {
            return _currentHero.inventory
                .Where(i => i.itemType == ItemType.Armor &&
                            InventoryUtils.GetItemData(i.itemId)?.itemCategory == itemCategory)
                .ToList();
        }

        return new List<InventoryItem>();
    }

    /// <summary>
    /// Obtiene todos los items del inventario.
    /// </summary>
    public static List<InventoryItem> GetAllItems()
    {
        return new List<InventoryItem>(_currentHero.inventory);
    }

    /// <summary>
    /// Cuenta la cantidad total de un item específico.
    /// </summary>
    public static int GetItemCount(string itemId)
    {
        return _currentHero.inventory
            .Where(item => item.itemId == itemId)
            .Sum(item => item.quantity);
    }

    /// <summary>
    /// Verifica si hay espacio disponible en el inventario.
    /// </summary>
    public static bool HasSpace()
    {
        Debug.Assert(_currentHero != null, "Current hero is not initialized");
        Debug.Assert(_currentHero.inventory != null, "Current hero inventory is not initialized");
        return _currentHero.inventory.Count < _inventoryLimit;
    }

    /// <summary>
    /// Obtiene el item en un slot específico.
    /// </summary>
    public static InventoryItem GetItemAtSlot(int slotIndex)
    {
        return _currentHero.inventory.FirstOrDefault(item => item.slotIndex == slotIndex);
    }

    #endregion

    #region Private Helper Methods

    private static InventoryItem FindStackableItem(string itemId)
    {
        return _currentHero.inventory.FirstOrDefault(item => 
            item.itemId == itemId && item.IsStackable);
    }

    private static bool IsSlotOccupied(int slotIndex)
    {
        return _currentHero.inventory.Any(item => item.slotIndex == slotIndex);
    }

    private static bool IsValidSlotIndex(int slotIndex)
    {
        bool isValid = slotIndex >= 0 && slotIndex < _inventoryLimit;
        if (!isValid) LogWarning($"Invalid slot index: {slotIndex}. Valid range: 0-{_inventoryLimit - 1}");
        return isValid;
    }

    private static void ValidateInventoryIntegrity()
    {
        if (_currentHero?.inventory == null) return;

        // Remover items nulos
        int removed = _currentHero.inventory.RemoveAll(item => item == null);
        if (removed > 0)
        {
            LogWarning($"Removed {removed} null items from inventory");
        }

        // Validar slots únicos para equipment
        var equipmentItems = _currentHero.inventory.Where(item => item.IsEquipment).ToList();
        var duplicateSlots = equipmentItems
            .GroupBy(item => item.slotIndex)
            .Where(group => group.Count() > 1)
            .ToList();

        foreach (var duplicateGroup in duplicateSlots)
        {
            LogWarning($"Found duplicate items in slot {duplicateGroup.Key}");
            // Reasignar slots a items duplicados
            var items = duplicateGroup.Skip(1).ToList();
            foreach (var item in items)
            {
                int newSlot = InventoryUtils.FindNextAvailableSlotIndex(_currentHero.inventory, _inventoryLimit);
                if (newSlot != -1)
                {
                    item.slotIndex = newSlot;
                    LogInfo($"Reassigned {item.itemId} to slot {newSlot}");
                }
            }
        }
    }

    #endregion

    #region Validation Methods

    private static bool ValidateAddOperation(InventoryItem item)
    {
        if (_currentHero?.inventory == null)
        {
            LogError("Inventory not initialized");
            return false;
        }

        if (item == null)
        {
            LogError("Cannot add null item");
            return false;
        }

        if (string.IsNullOrEmpty(item.itemId))
        {
            LogError("Item ID cannot be empty");
            return false;
        }

        return true;
    }

    private static bool ValidateRemoveOperation(InventoryItem item)
    {
        if (_currentHero?.inventory == null)
        {
            LogError("Inventory not initialized");
            return false;
        }

        if (item == null)
        {
            LogError("Cannot remove null item");
            return false;
        }

        if (!_currentHero.inventory.Contains(item))
        {
            LogWarning($"Item {item.itemId} not found in inventory");
            return false;
        }

        return true;
    }

    private static bool ValidateOperation(string itemId, int quantity)
    {
        if (_currentHero?.inventory == null)
        {
            LogError("Inventory not initialized");
            return false;
        }

        if (string.IsNullOrEmpty(itemId))
        {
            LogError("Item ID cannot be empty");
            return false;
        }

        if (quantity <= 0)
        {
            LogError($"Invalid quantity: {quantity}");
            return false;
        }

        return true;
    }

    #endregion

    #region Logging

    private static void LogInfo(string message) { Debug.Log($"[InventoryStorageService] {message}"); }
    private static void LogWarning(string message) { Debug.LogWarning($"[InventoryStorageService] {message}"); }
    private static void LogError(string message) { Debug.LogError($"[InventoryStorageService] {message}"); }

    #endregion
}
