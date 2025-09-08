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
        {
            Debug.LogWarning($"[ComparisonTooltipUtils] Tipo de equipamiento no válido para comparación: {equipmentType}");
            return false;
        }

        // Verificar si hay un ítem equipado en esa ranura
        var equippedItem = GetEquippedItemForComparison(equipmentType, itemData.itemCategory);
        if (equippedItem == null)
        {
            Debug.Log($"[ComparisonTooltipUtils] No hay ítem equipado para comparar en {equipmentType} - {itemData.itemCategory}");
            return false;
        }
        return equippedItem != null;
    }

    /// <summary>
    /// Obtiene el ítem equipado correspondiente para comparar con el ítem del inventario.
    /// </summary>
    /// <param name="equipmentType">Tipo de equipamiento</param>
    /// <returns>Datos del ítem equipado o null si no hay nada equipado</returns>
    public static InventoryItem GetEquippedItemForComparison(ItemType equipmentType, ItemCategory itemCategory)
    {
        try
        {
            // Obtener el ítem equipado usando el EquipmentManagerService
            var equippedItem = EquipmentManagerService.GetEquippedItem(equipmentType, itemCategory);
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
            case ItemType.Armor:
                return true;
            default:
                return false;
        }
    }
}
