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

    /// <summary>
    /// Inicializa el servicio con el héroe activo.
    /// </summary>
    public static void Initialize(HeroData hero)
    {
        _currentHero = hero ?? throw new ArgumentNullException(nameof(hero));
        LogInfo($"Consumable manager initialized for hero: {_currentHero.heroName}");
    }

    #region Consumable Usage

    /// <summary>
    /// Usa un consumible, aplicando sus efectos y reduciendo la cantidad.
    /// </summary>
    public static bool UseConsumable(InventoryItem item)
    {
        if (!ValidateConsumableOperation(item)) return false;

        ItemDataSO itemData = InventoryUtils.GetItemData(item.itemId);
        if (itemData == null)
        {
            LogError($"Item data not found for: {item.itemId}");
            return false;
        }

        // Aplicar efectos del consumible
        if (!ApplyConsumableEffects(itemData, item))
        {
            LogWarning($"Failed to apply effects for {item.itemId}");
            return false;
        }

        // Reducir cantidad
        item.quantity--;
        LogInfo($"Used {item.itemId}. Remaining quantity: {item.quantity}");

        // Si se agotó, remover del inventario
        if (item.quantity <= 0)
        {
            InventoryStorageService.RemoveSpecificItem(item);
            LogInfo($"Consumed all {item.itemId} - removed from inventory");
        }

        InventoryEventService.TriggerItemUsed(item);
        return true;
    }

    /// <summary>
    /// Usa múltiples unidades de un consumible de una vez.
    /// </summary>
    public static bool UseConsumableQuantity(InventoryItem item, int quantity)
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

        // Aplicar efectos múltiples veces
        for (int i = 0; i < quantity; i++)
        {
            if (!ApplyConsumableEffects(itemData, item))
            {
                LogWarning($"Failed to apply effects for {item.itemId} (iteration {i + 1})");
                // Continuar con las aplicaciones restantes
            }
        }

        // Reducir cantidad total
        item.quantity -= quantity;
        LogInfo($"Used {quantity}x {item.itemId}. Remaining quantity: {item.quantity}");

        // Si se agotó, remover del inventario
        if (item.quantity <= 0)
        {
            InventoryStorageService.RemoveSpecificItem(item);
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
    {
        if (!ItemInstanceService.CanStack(targetStack, sourceStack))
        {
            LogWarning($"Cannot combine stacks: {targetStack?.itemId} and {sourceStack?.itemId}");
            return false;
        }

        int totalQuantity = targetStack.quantity + sourceStack.quantity;
        
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
        var newStack = ItemInstanceService.CreateItem(originalStack.itemId, quantityToSplit);
        if (newStack == null)
        {
            LogError($"Failed to create new stack for {originalStack.itemId}");
            return false;
        }

        // Reducir cantidad del stack original
        originalStack.quantity -= quantityToSplit;

        // Agregar el nuevo stack al inventario
        if (!InventoryStorageService.AddExistingItem(newStack))
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

        if (item.quantity <= 1)
        {
            LogWarning("Cannot split stack with quantity <= 1");
            return false;
        }

        return true;
    }

    private static bool ApplyConsumableEffects(ItemDataSO itemData, InventoryItem item)
    {
        // Usar el sistema de efectos existente (ItemEffectSystem)
        bool effectsApplied = ItemEffectSystem.UseConsumableItem(item, _currentHero);
        
        if (effectsApplied) LogInfo($"Successfully applied effects for consumable: {item.itemId}");
        else LogWarning($"Failed to apply effects for consumable: {item.itemId}");
        
        return effectsApplied;
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
