using System;
using UnityEngine;
using Data.Items;

/// <summary>
/// Utilidades para determinar cuándo mostrar tooltips de comparación
/// y obtener el ítem equipado correspondiente.
/// </summary>
public static class ComparisonTooltipUtils
{
    /// <summary>
    /// Determina si se debe mostrar el tooltip de comparación para un ítem.
    /// </summary>
    /// <param name="itemData">Datos del ítem del inventario</param>
    /// <returns>True si se debe mostrar el tooltip de comparación</returns>
    public static bool ShouldShowComparison(ItemData itemData)
    {
        // Solo mostrar comparación para equipamiento
        if (itemData == null || !itemData.IsEquipment)
            return false;

        // Verificar que el tipo de equipo sea válido
        var equipmentType = itemData.itemType;
        if (!IsValidEquipmentTypeForComparison(equipmentType))
            return false;

        // Verificar si hay un ítem equipado en esa ranura
        var equippedItem = GetEquippedItemForComparison(equipmentType);
        return equippedItem != null;
    }

    /// <summary>
    /// Obtiene el ítem equipado correspondiente para comparar con el ítem del inventario.
    /// </summary>
    /// <param name="equipmentType">Tipo de equipamiento</param>
    /// <returns>Datos del ítem equipado o null si no hay nada equipado</returns>
    public static InventoryItem GetEquippedItemForComparison(ItemType equipmentType)
    {
        try
        {
            // Obtener el ítem equipado usando el EquipmentManagerService
            var equippedItem = EquipmentManagerService.GetEquippedItem(equipmentType);
            return equippedItem;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ComparisonTooltipUtils] Error obteniendo ítem equipado: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Verifica si el tipo de equipamiento es válido para comparación.
    /// </summary>
    /// <param name="equipmentType">Tipo de equipamiento</param>
    /// <returns>True si es válido para comparación</returns>
    private static bool IsValidEquipmentTypeForComparison(ItemType equipmentType)
    {
        // Lista de tipos de equipamiento que soportan comparación
        switch (equipmentType)
        {
            case ItemType.Weapon:
            case ItemType.Helmet:
            case ItemType.Torso:
            case ItemType.Gloves:
            case ItemType.Pants:
            case ItemType.Boots:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Calcula la posición offset para el tooltip de comparación basado en el tooltip principal.
    /// </summary>
    /// <param name="mainTooltipRect">RectTransform del tooltip principal</param>
    /// <param name="defaultOffset">Offset por defecto configurado</param>
    /// <returns>Vector2 con el offset calculado</returns>
    public static Vector2 CalculateComparisonOffset(RectTransform mainTooltipRect, Vector2 defaultOffset)
    {
        if (mainTooltipRect == null)
            return defaultOffset;

        // Posicionar el tooltip de comparación a la derecha del tooltip principal
        float tooltipWidth = mainTooltipRect.sizeDelta.x;
        return new Vector2(tooltipWidth + defaultOffset.x, defaultOffset.y);
    }

    /// <summary>
    /// Verifica si dos ítems son del mismo tipo de equipamiento y se pueden comparar.
    /// </summary>
    /// <param name="inventoryItem">Ítem del inventario</param>
    /// <param name="equippedItem">Ítem equipado</param>
    /// <returns>True si se pueden comparar</returns>
    public static bool CanCompareItems(InventoryItem inventoryItem, InventoryItem equippedItem)
    {
        if (inventoryItem == null || equippedItem == null)
            return false;

        return inventoryItem.itemType == equippedItem.itemType;
    }
}
