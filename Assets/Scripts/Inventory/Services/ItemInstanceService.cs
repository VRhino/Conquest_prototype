using System;
using System.Collections.Generic;
using UnityEngine;
using Data.Items;

/// <summary>
/// Servicio encargado de crear instancias de ítems, tanto equipment único como stackables.
/// Maneja la generación de stats para equipment y la lógica de stackeo para consumibles.
/// </summary>
public static class ItemInstanceService
{
    /// <summary>
    /// Crea una nueva instancia de ítem basada en el protoItem especificado.
    /// </summary>
    /// <param name="protoItemId">ID del protoItem de referencia</param>
    /// <param name="quantity">Cantidad inicial (solo relevante para stackables)</param>
    /// <returns>Nueva instancia de InventoryItem</returns>
    public static InventoryItem CreateItem(string protoItemId, int quantity = 1)
    {
        var protoItem = InventoryUtils.GetItemData(protoItemId);
        if (protoItem == null)
        {
            Debug.LogError($"[ItemInstanceService] ProtoItem not found: {protoItemId}");
            return null;
        }

        var item = new InventoryItem
        {
            itemId = protoItemId,
            itemType = protoItem.itemType,
            quantity = quantity,
            slotIndex = -1
        };

        // Para equipment: generar instancia única con stats
        if (protoItem.RequiresInstances)
        {
            item.instanceId = System.Guid.NewGuid().ToString();
            item.quantity = 1; // Equipment siempre es cantidad 1
            
            if (protoItem.statGenerator != null)
            {
                item.GeneratedStats = protoItem.statGenerator.GenerateStats();
                LogInfo($"Generated equipment instance: {protoItemId} with ID: {item.instanceId}");
            }
        }
        // Para consumables/stackables: no generar instancia
        else
        {
            item.instanceId = null;
            // No es necesario establecer GeneratedStats a null, queda como diccionario vacío
            LogInfo($"Created stackable item: {protoItemId} with quantity: {quantity}");
        }

        return item;
    }

    /// <summary>
    /// Verifica si dos ítems se pueden stackear juntos.
    /// </summary>
    /// <param name="item1">Primer ítem</param>
    /// <param name="item2">Segundo ítem</param>
    /// <returns>True si se pueden stackear</returns>
    public static bool CanStack(InventoryItem item1, InventoryItem item2)
    {
        if (item1 == null || item2 == null) 
            return false;

        // Ambos deben ser del mismo protoItem
        if (item1.itemId != item2.itemId) 
            return false;

        // Ambos deben ser stackables (sin instanceId)
        if (!item1.IsStackable || !item2.IsStackable) 
            return false;

        return true;
    }

    /// <summary>
    /// Combina dos ítems stackables en uno solo.
    /// </summary>
    /// <param name="targetItem">Ítem destino que recibirá la cantidad</param>
    /// <param name="sourceItem">Ítem origen que se consumirá</param>
    /// <returns>True si se combinaron exitosamente</returns>
    public static bool StackItems(InventoryItem targetItem, InventoryItem sourceItem)
    {
        if (!CanStack(targetItem, sourceItem))
        {
            LogWarning($"Cannot stack items: {targetItem?.itemId} and {sourceItem?.itemId}");
            return false;
        }

        targetItem.quantity += sourceItem.quantity;
        LogInfo($"Stacked {sourceItem.quantity} {sourceItem.itemId} into existing stack. New total: {targetItem.quantity}");
        
        return true;
    }

    /// <summary>
    /// Clona una instancia de equipment para crear una copia independiente.
    /// Útil para drops o recompensas basadas en equipment existente.
    /// </summary>
    /// <param name="originalItem">Ítem original a clonar</param>
    /// <returns>Nueva instancia clonada</returns>
    public static InventoryItem CloneEquipmentItem(InventoryItem originalItem)
    {
        if (originalItem == null || !originalItem.IsEquipment)
        {
            LogError("Cannot clone non-equipment item");
            return null;
        }

        var clonedItem = new InventoryItem
        {
            itemId = originalItem.itemId,
            itemType = originalItem.itemType,
            quantity = 1,
            slotIndex = -1,
            instanceId = System.Guid.NewGuid().ToString()
        };

        // Copiar los stats generados
        clonedItem.GeneratedStats = new Dictionary<string, float>(originalItem.GeneratedStats);

        LogInfo($"Cloned equipment item: {originalItem.itemId} -> {clonedItem.instanceId}");
        return clonedItem;
    }

    /// <summary>
    /// Genera stats aleatorios para un equipment existente.
    /// Útil para re-roll de stats o upgrades.
    /// </summary>
    /// <param name="equipmentItem">Equipment item a regenerar</param>
    /// <returns>True si se regeneraron exitosamente</returns>
    public static bool RegenerateStats(InventoryItem equipmentItem)
    {
        if (equipmentItem == null || !equipmentItem.IsEquipment)
        {
            LogError("Cannot regenerate stats for non-equipment item");
            return false;
        }

        var protoItem = InventoryUtils.GetItemData(equipmentItem.itemId);
        if (protoItem?.statGenerator == null)
        {
            LogError($"No stat generator found for item: {equipmentItem.itemId}");
            return false;
        }

        equipmentItem.GeneratedStats = protoItem.statGenerator.GenerateStats();
        LogInfo($"Regenerated stats for equipment: {equipmentItem.instanceId}");
        
        return true;
    }

    /// <summary>
    /// Obtiene información detallada de una instancia de ítem para debugging.
    /// </summary>
    /// <param name="item">Ítem a analizar</param>
    /// <returns>String con información detallada</returns>
    public static string GetItemInfo(InventoryItem item)
    {
        if (item == null) return "Item is null";

        var info = $"Item: {item.itemId}\n";
        info += $"Type: {item.itemType}\n";
        info += $"Quantity: {item.quantity}\n";
        info += $"Slot: {item.slotIndex}\n";
        info += $"Instance ID: {item.instanceId ?? "N/A"}\n";

        if (item.IsEquipment && item.GeneratedStats.Count > 0)
        {
            info += "Generated Stats:\n";
            foreach (var stat in item.GeneratedStats)
            {
                info += $"  {stat.Key}: {stat.Value:F2}\n";
            }
        }

        return info;
    }

    #region Logging
    private static void LogInfo(string message)
    {
        Debug.Log($"[ItemInstanceService] {message}");
    }

    private static void LogWarning(string message)
    {
        Debug.LogWarning($"[ItemInstanceService] {message}");
    }

    private static void LogError(string message)
    {
        Debug.LogError($"[ItemInstanceService] {message}");
    }
    #endregion
}
