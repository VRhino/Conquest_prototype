using System.Collections.Generic;
using System.Linq;
using Data.Items;
using UnityEngine;

/// <summary>
/// Servicio auxiliar para operaciones específicas de equipamiento.
/// Trabaja en conjunto con InventoryService para validaciones y lógica de equipos.
/// </summary>
public static class EquipmentService
{
    /// <summary>
    /// Obtiene todos los ítems equipados actualmente.
    /// </summary>
    /// <param name="hero">Datos del héroe</param>
    /// <returns>Lista de IDs de ítems equipados (excluye slots vacíos)</returns>
    public static List<string> GetAllEquippedItems(HeroData hero)
    {
        if (hero?.equipment == null) return new List<string>();

        var equippedItems = new List<string>();
        
        if (!string.IsNullOrEmpty(hero.equipment.weaponId)) equippedItems.Add(hero.equipment.weaponId);
        if (!string.IsNullOrEmpty(hero.equipment.helmetId)) equippedItems.Add(hero.equipment.helmetId);
        if (!string.IsNullOrEmpty(hero.equipment.torsoId)) equippedItems.Add(hero.equipment.torsoId);
        if (!string.IsNullOrEmpty(hero.equipment.glovesId)) equippedItems.Add(hero.equipment.glovesId);
        if (!string.IsNullOrEmpty(hero.equipment.pantsId)) equippedItems.Add(hero.equipment.pantsId);

        return equippedItems;
    }

    /// <summary>
    /// Verifica si un ítem específico está equipado.
    /// </summary>
    public static bool IsItemEquipped(HeroData hero, string itemId)
    {
        if (hero?.equipment == null || string.IsNullOrEmpty(itemId)) return false;

        return hero.equipment.weaponId == itemId ||
               hero.equipment.helmetId == itemId ||
               hero.equipment.torsoId == itemId ||
               hero.equipment.glovesId == itemId ||
               hero.equipment.pantsId == itemId;
    }

    /// <summary>
    /// Desequipa todos los ítems del héroe.
    /// </summary>
    public static void UnequipAll(HeroData hero)
    {
        if (hero?.equipment == null) return;

        hero.equipment.weaponId = string.Empty;
        hero.equipment.helmetId = string.Empty;
        hero.equipment.torsoId = string.Empty;
        hero.equipment.glovesId = string.Empty;
        hero.equipment.pantsId = string.Empty;
    }

    /// <summary>
    /// Valida si un ítem puede ser equipado por el héroe (validaciones de nivel, clase, etc.).
    /// </summary>
    public static bool CanEquipItem(HeroData hero, string itemId)
    {
        if (hero == null || string.IsNullOrEmpty(itemId)) return false;

        var itemData = InventoryUtils.GetItemData(itemId);
        if (itemData == null) return false;

        // Verificar que sea un tipo equipable
        if (!InventoryUtils.IsEquippableType(itemData.itemType)) return false;

        // Aquí se pueden agregar más validaciones en el futuro:
        // - Nivel requerido
        // - Clase de héroe
        // - Atributos mínimos
        // - Restricciones especiales

        return true;
    }

    /// <summary>
    /// Obtiene un resumen del equipamiento actual del héroe.
    /// </summary>
    public static EquipmentSummary GetEquipmentSummary(HeroData hero)
    {
        var summary = new EquipmentSummary();
        
        if (hero?.equipment == null) return summary;

        summary.WeaponEquipped = !string.IsNullOrEmpty(hero.equipment.weaponId);
        summary.HelmetEquipped = !string.IsNullOrEmpty(hero.equipment.helmetId);
        summary.TorsoEquipped = !string.IsNullOrEmpty(hero.equipment.torsoId);
        summary.GlovesEquipped = !string.IsNullOrEmpty(hero.equipment.glovesId);
        summary.PantsEquipped = !string.IsNullOrEmpty(hero.equipment.pantsId);

        summary.TotalSlotsUsed = GetAllEquippedItems(hero).Count;
        summary.TotalSlotsAvailable = 5; // Weapon, Helmet, Torso, Gloves, Pants

        return summary;
    }

    /// <summary>
    /// Valida que todos los ítems equipados existan en el inventario.
    /// </summary>
    public static bool ValidateEquippedItems(HeroData hero)
    {
        if (hero?.equipment == null || hero.inventory == null) return true;

        var equippedItems = GetAllEquippedItems(hero);
        foreach (var itemId in equippedItems)
        {
            bool foundInInventory = hero.inventory.Any(inv => inv.itemId == itemId);
            if (!foundInInventory)
            {
                Debug.LogWarning($"[EquipmentService] Equipped item '{itemId}' not found in inventory");
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Resumen del estado del equipamiento de un héroe.
/// </summary>
[System.Serializable]
public class EquipmentSummary
{
    public bool WeaponEquipped;
    public bool HelmetEquipped;
    public bool TorsoEquipped;
    public bool GlovesEquipped;
    public bool PantsEquipped;
    public int TotalSlotsUsed;
    public int TotalSlotsAvailable;

    public float EquipmentCompleteness => TotalSlotsAvailable > 0 ? (float)TotalSlotsUsed / TotalSlotsAvailable : 0f;
}
