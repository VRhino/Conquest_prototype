using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Data.Items;

/// <summary>
/// Utilidades centralizadas para el sistema de inventario.
/// Contiene funciones comunes utilizadas en múltiples archivos para evitar duplicación de código.
/// </summary>
public static class InventoryUtils
{
    #region Item Type Utilities

    /// <summary>
    /// Verifica si un tipo de ítem puede ser equipado.
    /// Centraliza la lógica de todos los tipos equipables.
    /// </summary>
    /// <param name="itemType">Tipo de ítem a verificar</param>
    /// <returns>True si el tipo es equipable</returns>
    public static bool IsEquippableType(ItemType itemType)
    {
        return itemType == ItemType.Weapon ||
                itemType == ItemType.Armor;
    }

    /// <summary>
    /// Verifica si un tipo de ítem es consumible.
    /// Centraliza la lógica de todos los tipos consumibles.
    /// </summary>
    /// <param name="itemType">Tipo de ítem a verificar</param>
    /// <returns>True si el tipo es consumible</returns>
    public static bool IsConsumableType(ItemType itemType)
    {
        return itemType == ItemType.Consumable;
    }

    #endregion

    #region Rarity Color System

    /// <summary>
    /// Diccionario de colores por rareza para mantener consistencia visual.
    /// </summary>
    public static readonly Dictionary<ItemRarity, Color> RarityColors = new()
    {
        { ItemRarity.Common,    new Color32(140, 140, 140, 255) }, // gray #8c8c8c
        { ItemRarity.Uncommon,  new Color32(72, 180, 122, 255) },  // green #48B47A
        { ItemRarity.Rare,      new Color32(89, 131, 203, 255) },  // blue #5983CB
        { ItemRarity.Epic,      new Color32(167, 108, 203, 255) }, // purple #A76CCB
        { ItemRarity.Legendary, new Color32(217, 159, 87, 255) }   // golden #D99F57
    };

    /// <summary>
    /// Obtiene el color de rareza para un InventoryItem.
    /// Centraliza la lógica de obtención de colores para evitar inconsistencias.
    /// </summary>
    /// <param name="item">Item del inventario</param>
    /// <returns>Color correspondiente a la rareza del ítem</returns>
    public static Color GetRarityColor(InventoryItem item)
    {
        if (item == null) return RarityColors[ItemRarity.Common];
        
        var itemData = GetItemData(item.itemId);
        if (itemData == null) return RarityColors[ItemRarity.Common];
        
        return GetRarityColor(itemData.rarity);
    }

    /// <summary>
    /// Obtiene el color de rareza directamente del enum ItemRarity.
    /// </summary>
    /// <param name="rarity">Rareza del ítem</param>
    /// <returns>Color correspondiente a la rareza</returns>
    public static Color GetRarityColor(ItemRarity rarity)
    {
        if (RarityColors.TryGetValue(rarity, out var color))
            return color;
        return RarityColors[ItemRarity.Common];
    }

    #endregion

    #region Database Access

    /// <summary>
    /// Obtiene la instancia segura de ItemDatabase con validación.
    /// Centraliza el acceso a la base de datos para evitar errores.
    /// </summary>
    /// <returns>Instancia de ItemDatabase o null si no existe</returns>
    public static ItemDatabase GetItemDatabase()
    {
        var database = ItemDatabase.Instance;
        if (database == null)
        {
            Debug.LogError("[InventoryUtils] ItemDatabase.Instance is null! Make sure ItemDatabase exists in Resources folder.");
        }
        return database;
    }

    /// <summary>
    /// Obtiene los datos de un ítem por su ID de forma segura.
    /// </summary>
    /// <param name="itemId">ID del ítem</param>
    /// <returns>Datos del ítem o null si no existe</returns>
    public static ItemData GetItemData(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        
        var database = GetItemDatabase();
        return database?.GetItemDataById(itemId);
    }

    /// <summary>
    /// Verifica si un ítem es stackeable.
    /// </summary>
    /// <param name="itemId">ID del ítem</param>
    /// <returns>True si es stackeable</returns>
    public static bool IsStackable(string itemId)
    {
        var itemData = GetItemData(itemId);
        return itemData != null && itemData.stackable;
    }

    #endregion

    #region Validation Utilities

    /// <summary>
    /// Valida parámetros comunes para operaciones de inventario.
    /// </summary>
    /// <param name="itemId">ID del ítem</param>
    /// <param name="quantity">Cantidad (opcional)</param>
    /// <returns>True si los parámetros son válidos</returns>
    public static bool ValidateItemParameters(string itemId, int quantity = 1)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            Debug.LogWarning("[InventoryUtils] ItemId cannot be null or empty");
            return false;
        }

        if (quantity <= 0)
        {
            Debug.LogWarning($"[InventoryUtils] Quantity must be positive, received: {quantity}");
            return false;
        }

        return true;
    }
    
    #endregion

    #region Tooltip Validation Utilities

    /// <summary>
    /// Verifica si un tooltip debería seguir visible basado en el estado actual del inventario.
    /// </summary>
    /// <param name="tooltipItem">Ítem mostrado en el tooltip</param>
    /// <param name="currentInventory">Inventario actual del héroe</param>
    /// <returns>True si el tooltip sigue siendo válido</returns>
    public static bool IsTooltipItemValid(InventoryItem tooltipItem, List<InventoryItem> currentInventory)
    {
        if (tooltipItem == null || currentInventory == null) return false;

        // Para equipment: verificar por instanceId (más preciso)
        if (!string.IsNullOrEmpty(tooltipItem.instanceId))
        {
            return currentInventory.Any(item => item.instanceId == tooltipItem.instanceId);
        }

        // Para stackables: verificar por itemId y slot position
        var currentItem = currentInventory.FirstOrDefault(item =>
            item.itemId == tooltipItem.itemId && item.slotIndex == tooltipItem.slotIndex);

        return currentItem != null;
    }

    /// <summary>
    /// Encuentra el item actualizado correspondiente a un tooltip item.
    /// </summary>
    /// <param name="tooltipItem">Ítem del tooltip a buscar</param>
    /// <param name="currentInventory">Inventario actual del héroe</param>
    /// <returns>Ítem actualizado o null si no existe</returns>
    public static InventoryItem GetUpdatedTooltipItem(InventoryItem tooltipItem, List<InventoryItem> currentInventory)
    {
        if (tooltipItem == null || currentInventory == null) return null;
        
        // Para equipment: buscar por instanceId
        if (!string.IsNullOrEmpty(tooltipItem.instanceId))
        {
            return currentInventory.FirstOrDefault(item => item.instanceId == tooltipItem.instanceId);
        }
        
        // Para otros: buscar por slotIndex (posición en la grilla)
        return currentInventory.FirstOrDefault(item => item.slotIndex == tooltipItem.slotIndex);
    }

    /// <summary>
    /// Compara si dos items han cambiado en propiedades relevantes para el tooltip.
    /// </summary>
    /// <param name="oldItem">Ítem original</param>
    /// <param name="newItem">Ítem nuevo</param>
    /// <returns>True si hay cambios significativos</returns>
    public static bool HasTooltipRelevantChanges(InventoryItem oldItem, InventoryItem newItem)
    {
        if (oldItem == null || newItem == null) return true;
        
        // Comparar propiedades básicas
        if (oldItem.itemId != newItem.itemId ||
            oldItem.quantity != newItem.quantity ||
            oldItem.slotIndex != newItem.slotIndex)
        {
            return true;
        }
        
        // Comparar stats si ambos items tienen stats
        var oldStats = oldItem.GeneratedStats;
        var newStats = newItem.GeneratedStats;
        return !AreStatDictionariesEqual(oldStats, newStats);
    }

    /// <summary>
    /// Compara dos diccionarios de stats para igualdad.
    /// </summary>
    /// <param name="stats1">Primer diccionario de stats</param>
    /// <param name="stats2">Segundo diccionario de stats</param>
    /// <returns>True si son iguales</returns>
    private static bool AreStatDictionariesEqual(Dictionary<string, float> stats1, Dictionary<string, float> stats2)
    {
        if (stats1 == null && stats2 == null) return true;
        if (stats1 == null || stats2 == null) return false;
        if (stats1.Count != stats2.Count) return false;
        
        foreach (var kvp in stats1)
        {
            if (!stats2.TryGetValue(kvp.Key, out float value) || 
                Mathf.Abs(kvp.Value - value) > 0.001f) // Tolerance for float comparison
            {
                return false;
            }
        }
        
        return true;
    }

    #endregion
    
    #region Instance Management Utilities

    /// <summary>
    /// Encuentra el próximo slot disponible en el inventario.
    /// </summary>
    /// <param name="inventory">Lista del inventario</param>
    /// <param name="maxSlots">Límite máximo de slots</param>
    /// <returns>Índice del slot disponible o -1 si no hay slots libres</returns>
    public static int FindNextAvailableSlotIndex(List<InventoryItem> inventory, int maxSlots)
    {
        if (inventory == null) return 0;
        
        // Crear un array para marcar slots ocupados
        bool[] occupiedSlots = new bool[maxSlots];
        
        // Marcar slots ocupados por ítems existentes
        foreach (var item in inventory)
        {
            if (item.slotIndex >= 0 && item.slotIndex < maxSlots)
            {
                occupiedSlots[item.slotIndex] = true;
            }
        }
        
        // Encontrar el primer slot libre
        for (int i = 0; i < maxSlots; i++)
        {
            if (!occupiedSlots[i])
            {
                return i;
            }
        }
        
        return -1; // No hay slots libres
    }

    #endregion
}
