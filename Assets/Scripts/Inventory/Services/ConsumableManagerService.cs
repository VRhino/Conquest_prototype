using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Data.Items;

/// <summary>
/// Servicio especializado en manejo de consumibles y items stackeables.
/// Gestiona uso de consumibles, efectos y lógica de stackeo.
/// </summary>
public static class ConsumableManagerService
{
    private static HeroData _currentHero;
    private static readonly HashSet<InventoryItem> pendingUses = new();
    public static void Reset() { _currentHero = null; pendingUses.Clear(); }

    /// <summary>
    /// Inicializa el servicio con el héroe activo.
    /// </summary>
    public static void Initialize(HeroData hero)
    {
        if (!ReferenceEquals(_currentHero, hero)) pendingUses.Clear();
        _currentHero = hero ?? throw new ArgumentNullException(nameof(hero));
        LogInfo($"Consumable manager initialized for hero: {_currentHero.heroName}");
    }

    #region Consumable Usage

    /// <summary>
    /// Usa un consumible, aplicando sus efectos y reduciendo la cantidad.
    /// </summary>
    public static bool UseConsumable(InventoryItem item)
        => UseConsumableQuantity(item, 1);

    /// <summary>
    /// Usa múltiples unidades de un consumible de una vez.
    /// </summary>
    public static bool UseConsumableQuantity(InventoryItem item, int quantity)
    {
        if (!ValidateConsumableOperation(item) || quantity <= 0 || quantity > item.quantity || pendingUses.Contains(item)) return false;
        var interactive = InventoryUtils.GetItemData(item.itemId).effects?.Where(effect => effect != null && effect.RequiresSelection).ToArray();
        if (interactive != null && interactive.Length != 0)
        {
            if (interactive.Length != 1 || quantity != 1) return false;
            var hero = _currentHero;
            pendingUses.Add(item);
            bool opened = interactive[0].BeginSelection(hero, selected =>
            {
                if (!ReferenceEquals(hero, _currentHero) || !pendingUses.Contains(item)) return false;
                bool success = InventoryEventService.RunTransaction(() => UseConsumableQuantityCore(item, quantity, interactive[0], selected));
                if (success) pendingUses.Remove(item);
                return success;
            }, () => pendingUses.Remove(item));
            if (!opened) pendingUses.Remove(item);
            return opened;
        }
        return InventoryEventService.RunTransaction(() => UseConsumableQuantityCore(item, quantity));
    }

    private static bool UseConsumableQuantityCore(InventoryItem item, int quantity,
        ItemEffect selectedEffect = null, List<string> selectedItems = null)
    {
        if (!ValidateConsumableOperation(item)) return false;

        if (quantity <= 0)
        {
            LogWarning($"Invalid quantity to use: {quantity}");
            return false;
        }

        if (item.quantity < quantity)
        {
            LogWarning($"Insufficient quantity. Required: {quantity}, Available: {item.quantity}");
            return false;
        }

        ItemDataSO itemData = InventoryUtils.GetItemData(item.itemId);
        if (itemData == null)
        {
            LogError($"Item data not found for: {item.itemId}");
            return false;
        }

        // Reserve consumption first so a reward can use the slot released by the final unit.
        if (itemData.consumeOnUse) item.quantity -= quantity;
        if (item.quantity == 0 && !InventoryStorageService.RemoveSpecificItem(item)) return false;

        // Aplicar efectos múltiples veces
        for (int i = 0; i < quantity; i++)
        {
            if (!ItemEffectSystem.UseConsumableItem(item, _currentHero, selectedEffect, selectedItems))
            {
                LogWarning($"Failed to apply effects for {item.itemId} (iteration {i + 1})");
                return false;
            }
        }

        // Reducir cantidad total
        LogInfo($"Used {quantity}x {item.itemId}. Remaining quantity: {item.quantity}");

        // Si se agotó, remover del inventario
        if (item.quantity <= 0)
        {
            LogInfo($"Consumed all {item.itemId} - removed from inventory");
        }

        InventoryEventService.TriggerItemUsed(item);
        return true;
    }

    #endregion

    #region Stack Management

    /// <summary>
    /// Combina dos stacks del mismo item.
    /// </summary>
    public static bool CombineStacks(InventoryItem targetStack, InventoryItem sourceStack)
        => InventoryEventService.RunTransaction(() => CombineStacksCore(targetStack, sourceStack));

    private static bool CombineStacksCore(InventoryItem targetStack, InventoryItem sourceStack)
    {
        if (!InventoryStorageService.Contains(targetStack) || !InventoryStorageService.Contains(sourceStack)) return false;
        if (!ItemInstanceService.CanStack(targetStack, sourceStack))
        {
            LogWarning($"Cannot combine stacks: {targetStack?.itemId} and {sourceStack?.itemId}");
            return false;
        }

        long totalQuantity = (long)targetStack.quantity + sourceStack.quantity;
        
        // Verificar límite máximo de stack (sin límite específico en ItemData, usar valor alto)
        var itemData = InventoryUtils.GetItemData(targetStack.itemId);
        int maxStackSize = int.MaxValue; // Los items stackeables no tienen límite específico

        if (totalQuantity > maxStackSize)
        {
            LogWarning($"Cannot combine - would exceed max stack size ({maxStackSize})");
            return false;
        }

        // Realizar la combinación
        if (ItemInstanceService.StackItems(targetStack, sourceStack))
        {
            // Remover el stack origen (ya vacío)
            InventoryStorageService.RemoveSpecificItem(sourceStack);
            LogInfo($"Combined stacks: {targetStack.itemId} (total: {targetStack.quantity})");
            
            InventoryEventService.TriggerInventoryChanged();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Divide un stack en dos partes.
    /// </summary>
    public static bool SplitStack(InventoryItem originalStack, int quantityToSplit)
        => InventoryEventService.RunTransaction(() => SplitStackCore(originalStack, quantityToSplit));

    private static bool SplitStackCore(InventoryItem originalStack, int quantityToSplit)
    {
        if (!ValidateStackOperation(originalStack)) return false;

        if (quantityToSplit <= 0 || quantityToSplit >= originalStack.quantity)
        {
            LogWarning($"Invalid split quantity: {quantityToSplit} (stack has {originalStack.quantity})");
            return false;
        }

        if (!InventoryStorageService.HasSpace())
        {
            LogWarning("Cannot split stack - no available inventory slots");
            return false;
        }

        // Crear nuevo stack con la cantidad dividida
        var newStack = new InventoryItem { itemId = originalStack.itemId, itemType = originalStack.itemType,
            quantity = quantityToSplit, price = originalStack.price };
        if (newStack == null)
        {
            LogError($"Failed to create new stack for {originalStack.itemId}");
            return false;
        }

        // Reducir cantidad del stack original
        originalStack.quantity -= quantityToSplit;

        // Agregar el nuevo stack al inventario
        int slot = InventoryUtils.FindNextAvailableSlotIndex(_currentHero.inventory, InventoryStorageService.InventoryLimit);
        if (!InventoryStorageService.AddExistingItemAtSlot(newStack, slot))
        {
            LogError("Failed to add new stack to inventory");
            // Revertir cambios
            originalStack.quantity += quantityToSplit;
            return false;
        }

        LogInfo($"Split stack: {originalStack.itemId} ({originalStack.quantity} + {newStack.quantity})");
        InventoryEventService.TriggerInventoryChanged();
        
        return true;
    }

    #endregion

    #region Consumable Validation

    /// <summary>
    /// Verifica si un item puede ser usado como consumible.
    /// </summary>
    public static bool CanUseItem(InventoryItem item)
    {
        if (item == null)
        {
            LogWarning("Cannot use null item");
            return false;
        }

        if (!item.IsStackable)
        {
            LogWarning($"Item {item.itemId} is not a consumable");
            return false;
        }

        ItemDataSO itemData = InventoryUtils.GetItemData(item.itemId);
        if (itemData == null)
        {
            LogWarning($"Item data not found for: {item.itemId}");
            return false;
        }

        if (!InventoryUtils.IsConsumableType(itemData.itemType))
        {
            LogWarning($"Item type {itemData.itemType} is not consumable");
            return false;
        }

        if (item.quantity <= 0)
        {
            LogWarning($"Item {item.itemId} has no quantity to consume");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Obtiene todos los consumibles disponibles en el inventario.
    /// </summary>
    public static List<InventoryItem> GetAvailableConsumables()
    {
        var allItems = InventoryStorageService.GetAllItems();
        return allItems.Where(item => CanUseItem(item)).ToList();
    }

    /// <summary>
    /// Cuenta la cantidad total de un consumible específico.
    /// </summary>
    public static int GetConsumableCount(string itemId)
    {
        return InventoryStorageService.GetItemCount(itemId);
    }

    #endregion

    #region Private Helper Methods

    private static bool ValidateConsumableOperation(InventoryItem item)
    {
        if (_currentHero?.inventory == null)
        {
            LogError("Inventory not initialized");
            return false;
        }

        if (!CanUseItem(item))
        {
            return false;
        }

        // Verificar que el item está en el inventario
        var inventoryItems = InventoryStorageService.GetAllItems();
        bool itemInInventory = inventoryItems.Contains(item);

        if (!itemInInventory)
        {
            LogWarning($"Item {item.itemId} not found in inventory");
            return false;
        }

        return true;
    }

    private static bool ValidateStackOperation(InventoryItem item)
    {
        if (_currentHero?.inventory == null)
        {
            LogError("Inventory not initialized");
            return false;
        }

        if (item == null || !item.IsStackable)
        {
            LogWarning("Item is not stackable");
            return false;
        }

        if (!InventoryStorageService.Contains(item)) return false;

        if (item.quantity <= 1)
        {
            LogWarning("Cannot split stack with quantity <= 1");
            return false;
        }

        return true;
    }

    #endregion

    #region Debugging and Reporting

    /// <summary>
    /// Genera un reporte de todos los consumibles disponibles.
    /// </summary>
    public static string GenerateConsumablesReport()
    {
        var consumables = GetAvailableConsumables();
        
        var report = new System.Text.StringBuilder();
        report.AppendLine($"=== CONSUMABLES REPORT - {_currentHero?.heroName} ===");
        report.AppendLine($"Total Consumable Types: {consumables.Count}");
        report.AppendLine();

        if (consumables.Count == 0)
        {
            report.AppendLine("No consumables available");
            return report.ToString();
        }

        // Agrupar por tipo de item
        var groupedConsumables = consumables.GroupBy(item => item.itemId);
        foreach (var group in groupedConsumables)
        {
            int totalQuantity = group.Sum(item => item.quantity);
            int stackCount = group.Count();
            
            report.AppendLine($"{group.Key}:");
            report.AppendLine($"  Total Quantity: {totalQuantity}");
            report.AppendLine($"  Number of Stacks: {stackCount}");
            
            foreach (var item in group)
            {
                report.AppendLine($"    Slot {item.slotIndex}: {item.quantity} units");
            }
            report.AppendLine();
        }

        return report.ToString();
    }

    #endregion

    #region Logging

    private static void LogInfo(string message)
    {
        Debug.Log($"[ConsumableManagerService] {message}");
    }

    private static void LogWarning(string message)
    {
        Debug.LogWarning($"[ConsumableManagerService] {message}");
    }

    private static void LogError(string message)
    {
        Debug.LogError($"[ConsumableManagerService] {message}");
    }

    #endregion
}
