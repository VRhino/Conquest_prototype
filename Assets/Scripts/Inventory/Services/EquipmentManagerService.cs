using System;
using System.Linq;
using UnityEngine;
using Data.Items;

/// <summary>
/// Servicio especializado en manejo de equipamiento del héroe.
/// Gestiona equipar, desequipar y validaciones específicas de equipment.
/// </summary>
public static class EquipmentManagerService
{
    private static HeroData _currentHero;

    /// <summary>
    /// Inicializa el servicio con el héroe activo.
    /// </summary>
    public static void Initialize(HeroData hero)
    {
        _currentHero = hero ?? throw new ArgumentNullException(nameof(hero));
        
        if (_currentHero.equipment == null)
        {
            _currentHero.equipment = new Equipment();
            LogInfo("Initialized empty equipment for hero");
        }
        
        LogInfo($"Equipment manager initialized for hero: {_currentHero.heroName}");
    }

    #region Equipment Operations

    /// <summary>
    /// Equipa un item del inventario. Maneja intercambio automático si ya hay algo equipado.
    /// </summary>
    public static bool EquipItem(InventoryItem item)
    {
        if (!ValidateEquipOperation(item)) return false;

        var itemData = InventoryUtils.GetItemData(item.itemId);
        if (itemData == null)
        {
            LogError($"Item data not found for: {item.itemId}");
            return false;
        }

        // PASO 1: Recordar el slot original y remover del inventario PRIMERO
        int originalSlot = item.slotIndex;
        if (!InventoryStorageService.RemoveSpecificItem(item))
        {
            LogError($"Failed to remove {item.itemId} from inventory slot {originalSlot}");
            return false;
        }

        // PASO 2: Obtener el item actualmente equipado en este slot
        var currentlyEquipped = GetEquippedItem(itemData.itemType);
        
        // PASO 3: Equipar el nuevo item
        if (!SetEquippedItem(itemData.itemType, item))
        {
            // Si falla, devolver el item al inventario para mantener consistencia
            InventoryStorageService.AddExistingItemAtSlot(item, originalSlot);
            LogError($"Failed to equip item in slot: {itemData.itemType}. Item returned to inventory.");
            return false;
        }

        // PASO 4: Si había algo equipado, colocarlo en el slot liberado
        if (currentlyEquipped != null)
        {
            if (!InventoryStorageService.AddExistingItemAtSlot(currentlyEquipped, originalSlot))
            {
                // Si no se puede colocar en el slot específico, buscar cualquier slot disponible
                if (!InventoryStorageService.AddExistingItem(currentlyEquipped))
                {
                    // Si falla completamente, revertir el equipamiento
                    SetEquippedItem(itemData.itemType, currentlyEquipped);
                    InventoryStorageService.AddExistingItemAtSlot(item, originalSlot);
                    LogError($"Failed to place {currentlyEquipped.itemId} in inventory. Equipment reverted.");
                    return false;
                }
                LogWarning($"Could not place {currentlyEquipped.itemId} in original slot {originalSlot}, placed in available slot instead");
            }
            
            LogInfo($"Swapped {item.itemId} (equipped) with {currentlyEquipped.itemId} (to slot {originalSlot})");
            InventoryEventService.TriggerItemEquipped(item, currentlyEquipped);
        }
        else
        {
            LogInfo($"Equipped {item.itemId} from slot {originalSlot} in empty {itemData.itemType} slot");
            InventoryEventService.TriggerItemEquipped(item);
        }
        
        return true;
    }

    /// <summary>
    /// Desequipa un item de un slot específico y lo devuelve al inventario.
    /// </summary>
    public static bool UnequipItem(ItemType itemType)
    {
        var equippedItem = GetEquippedItem(itemType);
        if (equippedItem == null)
        {
            LogWarning($"No item equipped in slot: {itemType}");
            return false;
        }

        // Verificar que hay espacio en el inventario
        if (!InventoryStorageService.HasSpace())
        {
            LogWarning("Cannot unequip - no space in inventory");
            return false;
        }

        // Remover del equipment
        if (!SetEquippedItem(itemType, null))
        {
            LogError($"Failed to unequip item from slot: {itemType}");
            return false;
        }

        // Devolver al inventario
        if (!InventoryStorageService.AddExistingItem(equippedItem))
        {
            // Revertir si no se puede agregar al inventario
            SetEquippedItem(itemType, equippedItem);
            LogError($"Failed to add {equippedItem.itemId} back to inventory. Unequip reverted.");
            return false;
        }

        LogInfo($"Unequipped {equippedItem.itemId} from {itemType} slot");
        InventoryEventService.TriggerItemUnequipped(equippedItem);
        
        return true;
    }

    /// <summary>
    /// Intercambia directamente dos items de equipment sin pasar por el inventario.
    /// </summary>
    public static bool SwapEquipment(ItemType slotType1, ItemType slotType2)
    {
        var item1 = GetEquippedItem(slotType1);
        var item2 = GetEquippedItem(slotType2);

        if (item1 == null && item2 == null)
        {
            LogWarning("Cannot swap - both slots are empty");
            return false;
        }

        // Verificar que los items pueden equiparse en los slots destino
        if (item1 != null && !CanEquipInSlot(item1, slotType2))
        {
            LogWarning($"{item1.itemId} cannot be equipped in {slotType2} slot");
            return false;
        }

        if (item2 != null && !CanEquipInSlot(item2, slotType1))
        {
            LogWarning($"{item2.itemId} cannot be equipped in {slotType1} slot");
            return false;
        }

        // Realizar el intercambio
        SetEquippedItem(slotType1, item2);
        SetEquippedItem(slotType2, item1);

        string swapInfo = $"Swapped equipment: {slotType1} <-> {slotType2}";
        LogInfo(swapInfo);
        InventoryEventService.LogInventoryOperation(swapInfo);

        return true;
    }

    #endregion

    #region Equipment Queries

    /// <summary>
    /// Obtiene el item equipado en un slot específico.
    /// </summary>
    public static InventoryItem GetEquippedItem(ItemType itemType)
    {
        if (_currentHero?.equipment == null) return null;

        return itemType switch
        {
            ItemType.Weapon => _currentHero.equipment.weapon,
            ItemType.Helmet => _currentHero.equipment.helmet,
            ItemType.Torso => _currentHero.equipment.torso,
            ItemType.Gloves => _currentHero.equipment.gloves,
            ItemType.Pants => _currentHero.equipment.pants,
            ItemType.Boots => _currentHero.equipment.boots,
            _ => null
        };
    }

    /// <summary>
    /// Verifica si un item específico está equipado.
    /// </summary>
    public static bool IsItemEquipped(InventoryItem item)
    {
        if (item?.IsEquipment != true) return false;

        var itemData = InventoryUtils.GetItemData(item.itemId);
        if (itemData == null) return false;

        var equippedItem = GetEquippedItem(itemData.itemType);
        return equippedItem?.instanceId == item.instanceId;
    }

    /// <summary>
    /// Obtiene todos los items equipados como array.
    /// </summary>
    public static InventoryItem[] GetAllEquippedItems()
    {
        if (_currentHero?.equipment == null) return new InventoryItem[0];

        return new InventoryItem[]
        {
            _currentHero.equipment.weapon,
            _currentHero.equipment.helmet,
            _currentHero.equipment.torso,
            _currentHero.equipment.gloves,
            _currentHero.equipment.pants,
            _currentHero.equipment.boots
        };
    }

    /// <summary>
    /// Cuenta cuántos slots de equipment están ocupados.
    /// </summary>
    public static int GetEquippedItemCount()
    {
        var equippedItems = GetAllEquippedItems();
        return equippedItems.Count(item => item != null);
    }

    #endregion

    #region Validation Methods

    /// <summary>
    /// Verifica si un item puede ser equipado.
    /// </summary>
    public static bool CanEquipItem(InventoryItem item)
    {
        if (item == null)
        {
            LogWarning("Cannot equip null item");
            return false;
        }

        if (!item.IsEquipment)
        {
            LogWarning($"Item {item.itemId} is not equipment");
            return false;
        }

        var itemData = InventoryUtils.GetItemData(item.itemId);
        if (itemData == null)
        {
            LogWarning($"Item data not found for: {item.itemId}");
            return false;
        }

        if (!InventoryUtils.IsEquippableType(itemData.itemType))
        {
            LogWarning($"Item type {itemData.itemType} is not equippable");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Verifica si un item puede equiparse en un slot específico.
    /// </summary>
    public static bool CanEquipInSlot(InventoryItem item, ItemType slotType)
    {
        if (!CanEquipItem(item)) return false;

        var itemData = InventoryUtils.GetItemData(item.itemId);
        if (itemData == null) return false;

        return itemData.itemType == slotType;
    }

    /// <summary>
    /// Calcula el total de stats proporcionados por todo el equipment equipado.
    /// </summary>
    public static System.Collections.Generic.Dictionary<string, float> CalculateTotalEquipmentStats()
    {
        var totalStats = new System.Collections.Generic.Dictionary<string, float>();
        var equippedItems = GetAllEquippedItems();

        foreach (var item in equippedItems)
        {
            if (item?.IsEquipment != true) continue;

            foreach (var stat in item.GeneratedStats)
            {
                if (totalStats.ContainsKey(stat.Key))
                {
                    totalStats[stat.Key] += stat.Value;
                }
                else
                {
                    totalStats[stat.Key] = stat.Value;
                }
            }
        }

        return totalStats;
    }

    #endregion

    #region Private Helper Methods

    private static bool ValidateEquipOperation(InventoryItem item)
    {
        if (_currentHero?.equipment == null)
        {
            LogError("Equipment not initialized");
            return false;
        }

        if (!CanEquipItem(item))
        {
            return false;
        }

        // Verificar que el item está en el inventario
        var inventoryItems = InventoryStorageService.GetAllItems();
        bool itemInInventory = inventoryItems.Any(invItem => 
            invItem.instanceId == item.instanceId);

        if (!itemInInventory)
        {
            LogWarning($"Item {item.itemId} not found in inventory");
            return false;
        }

        return true;
    }

    private static bool SetEquippedItem(ItemType itemType, InventoryItem item)
    {
        if (_currentHero?.equipment == null) return false;

        switch (itemType)
        {
            case ItemType.Weapon:
                _currentHero.equipment.weapon = item;
                return true;
            case ItemType.Helmet:
                _currentHero.equipment.helmet = item;
                return true;
            case ItemType.Torso:
                _currentHero.equipment.torso = item;
                return true;
            case ItemType.Gloves:
                _currentHero.equipment.gloves = item;
                return true;
            case ItemType.Pants:
                _currentHero.equipment.pants = item;
                return true;
            case ItemType.Boots:
                _currentHero.equipment.boots = item;
                return true;
            default:
                LogError($"Invalid equipment slot: {itemType}");
                return false;
        }
    }

    #endregion

    #region Debugging and Reporting

    /// <summary>
    /// Genera un reporte detallado del equipment actual.
    /// </summary>
    public static string GenerateEquipmentReport()
    {
        if (_currentHero?.equipment == null)
        {
            return "Equipment not initialized";
        }

        var report = new System.Text.StringBuilder();
        report.AppendLine($"=== EQUIPMENT REPORT - {_currentHero.heroName} ===");
        report.AppendLine($"Equipped Items: {GetEquippedItemCount()}/6");
        report.AppendLine();

        AppendEquipmentSlotInfo(report, "Weapon", _currentHero.equipment.weapon);
        AppendEquipmentSlotInfo(report, "Helmet", _currentHero.equipment.helmet);
        AppendEquipmentSlotInfo(report, "Torso", _currentHero.equipment.torso);
        AppendEquipmentSlotInfo(report, "Gloves", _currentHero.equipment.gloves);
        AppendEquipmentSlotInfo(report, "Pants", _currentHero.equipment.pants);
        AppendEquipmentSlotInfo(report, "Boots", _currentHero.equipment.boots);

        // Stats totales
        var totalStats = CalculateTotalEquipmentStats();
        if (totalStats.Count > 0)
        {
            report.AppendLine("=== TOTAL EQUIPMENT STATS ===");
            foreach (var stat in totalStats)
            {
                report.AppendLine($"{stat.Key}: {stat.Value:F2}");
            }
        }

        return report.ToString();
    }

    private static void AppendEquipmentSlotInfo(System.Text.StringBuilder report, string slotName, InventoryItem item)
    {
        report.AppendLine($"[{slotName}]");
        if (item != null)
        {
            report.AppendLine($"  Item: {item.itemId}");
            report.AppendLine($"  Instance ID: {item.instanceId}");
            if (item.GeneratedStats.Count > 0)
            {
                report.AppendLine("  Stats:");
                foreach (var stat in item.GeneratedStats)
                {
                    report.AppendLine($"    {stat.Key}: {stat.Value:F2}");
                }
            }
        }
        else
        {
            report.AppendLine("  Empty");
        }
        report.AppendLine();
    }

    #endregion

    #region Logging

    private static void LogInfo(string message)
    {
        Debug.Log($"[EquipmentManagerService] {message}");
    }

    private static void LogWarning(string message)
    {
        Debug.LogWarning($"[EquipmentManagerService] {message}");
    }

    private static void LogError(string message)
    {
        Debug.LogError($"[EquipmentManagerService] {message}");
    }

    #endregion
}
