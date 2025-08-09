using System.Collections.Generic;
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
               itemType == ItemType.Helmet ||
               itemType == ItemType.Torso ||
               itemType == ItemType.Gloves ||
               itemType == ItemType.Pants ||
               itemType == ItemType.Boots;
    }

    /// <summary>
    /// Obtiene todos los tipos de ítems equipables.
    /// </summary>
    /// <returns>Array con todos los tipos equipables</returns>
    public static ItemType[] GetAllEquippableTypes()
    {
        return new ItemType[]
        {
            ItemType.Weapon,
            ItemType.Helmet,
            ItemType.Torso,
            ItemType.Gloves,
            ItemType.Pants,
            ItemType.Boots
        };
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
    /// Verifica si un ítem existe en la base de datos.
    /// </summary>
    /// <param name="itemId">ID del ítem</param>
    /// <returns>True si el ítem existe</returns>
    public static bool ItemExists(string itemId)
    {
        return GetItemData(itemId) != null;
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

    #region Equipment Slot Management

    /// <summary>
    /// Obtiene el ID del ítem equipado en un slot específico.
    /// Centraliza el acceso a los diferentes slots de equipamiento.
    /// </summary>
    /// <param name="equipment">Equipamiento del héroe</param>
    /// <param name="itemType">Tipo de slot</param>
    /// <returns>ID del ítem equipado o string vacío si no hay nada</returns>
    public static string GetEquippedItemId(Equipment equipment, ItemType itemType)
    {
        if (equipment == null) return string.Empty;

        return itemType switch
        {
            ItemType.Weapon => equipment.weaponId ?? string.Empty,
            ItemType.Helmet => equipment.helmetId ?? string.Empty,
            ItemType.Torso => equipment.torsoId ?? string.Empty,
            ItemType.Gloves => equipment.glovesId ?? string.Empty,
            ItemType.Pants => equipment.pantsId ?? string.Empty,
            ItemType.Boots => equipment.bootsId ?? string.Empty,
            _ => string.Empty
        };
    }

    /// <summary>
    /// Asigna un ítem a un slot específico de equipamiento.
    /// </summary>
    /// <param name="equipment">Equipamiento del héroe</param>
    /// <param name="itemType">Tipo de slot</param>
    /// <param name="itemId">ID del ítem a equipar</param>
    public static void SetEquippedItemId(Equipment equipment, ItemType itemType, string itemId)
    {
        if (equipment == null) return;

        switch (itemType)
        {
            case ItemType.Weapon:
                equipment.weaponId = itemId;
                break;
            case ItemType.Helmet:
                equipment.helmetId = itemId;
                break;
            case ItemType.Torso:
                equipment.torsoId = itemId;
                break;
            case ItemType.Gloves:
                equipment.glovesId = itemId;
                break;
            case ItemType.Pants:
                equipment.pantsId = itemId;
                break;
            case ItemType.Boots:
                equipment.bootsId = itemId;
                break;
        }
    }

    /// <summary>
    /// Obtiene el slot donde está equipado un ítem específico.
    /// </summary>
    /// <param name="equipment">Equipamiento del héroe</param>
    /// <param name="itemId">ID del ítem a buscar</param>
    /// <returns>Tipo de slot donde está equipado o ItemType.None si no está equipado</returns>
    public static ItemType GetEquippedSlot(Equipment equipment, string itemId)
    {
        if (equipment == null || string.IsNullOrEmpty(itemId)) return ItemType.None;

        if (equipment.weaponId == itemId) return ItemType.Weapon;
        if (equipment.helmetId == itemId) return ItemType.Helmet;
        if (equipment.torsoId == itemId) return ItemType.Torso;
        if (equipment.glovesId == itemId) return ItemType.Gloves;
        if (equipment.pantsId == itemId) return ItemType.Pants;
        if (equipment.bootsId == itemId) return ItemType.Boots;

        return ItemType.None;
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

    /// <summary>
    /// Valida que un héroe tenga datos válidos para operaciones de inventario.
    /// </summary>
    /// <param name="hero">Datos del héroe</param>
    /// <returns>True si el héroe es válido</returns>
    public static bool ValidateHeroData(HeroData hero)
    {
        if (hero == null)
        {
            Debug.LogWarning("[InventoryUtils] Hero data cannot be null");
            return false;
        }

        if (hero.inventory == null)
        {
            Debug.LogWarning("[InventoryUtils] Hero inventory list cannot be null");
            return false;
        }

        return true;
    }

    #endregion

    #region Statistics Utilities

    /// <summary>
    /// Cuenta los ítems por tipo en una lista de inventario.
    /// Centraliza la lógica de conteo para estadísticas.
    /// </summary>
    /// <param name="items">Lista de ítems del inventario</param>
    /// <returns>Diccionario con conteos por tipo</returns>
    public static Dictionary<ItemType, int> CountItemsByType(List<InventoryItem> items)
    {
        var counts = new Dictionary<ItemType, int>();
        
        // Inicializar contadores
        foreach (var itemType in GetAllEquippableTypes())
        {
            counts[itemType] = 0;
        }
        counts[ItemType.Consumable] = 0;
        counts[ItemType.Visual] = 0;

        // Contar ítems
        if (items != null)
        {
            foreach (var item in items)
            {
                var itemData = GetItemData(item.itemId);
                if (itemData != null)
                {
                    var type = itemData.itemType;
                    if (counts.ContainsKey(type))
                    {
                        counts[type]++;
                    }
                }
            }
        }

        return counts;
    }

    #endregion

    #region Debugging and Logging

    /// <summary>
    /// Genera un log de debug para operaciones de inventario.
    /// </summary>
    /// <param name="operation">Nombre de la operación</param>
    /// <param name="itemId">ID del ítem</param>
    /// <param name="quantity">Cantidad</param>
    /// <param name="success">Si la operación fue exitosa</param>
    public static void LogInventoryOperation(string operation, string itemId, int quantity = 1, bool success = true)
    {
        var status = success ? "SUCCESS" : "FAILED";
        var itemName = GetItemData(itemId)?.name ?? itemId;
        Debug.Log($"[InventoryUtils] {operation} - {status}: {itemName} x{quantity}");
    }

    #endregion
}
