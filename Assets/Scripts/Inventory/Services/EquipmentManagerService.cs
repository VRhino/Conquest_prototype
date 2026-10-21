using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using Data.Items;

/// <summary>
/// Servicio especializado en manejo de equipamiento del héroe.
/// Gestiona equipar, desequipar y validaciones específicas de equipment.
/// </summary>
public static class EquipmentManagerService
{
    private static IHeroInventoryMutator _currentHero;
    public static void Reset() => _currentHero = null;

    #region Equipment Access Dictionaries

    private static readonly Dictionary<(ItemType, ItemCategory), System.Func<InventoryItem>> _equipmentGetters = new()
    {
        { (ItemType.Weapon, ItemCategory.None), () => _currentHero.Equipment.weapon },
        { (ItemType.Armor, ItemCategory.Helmet), () => _currentHero.Equipment.helmet },
        { (ItemType.Armor, ItemCategory.Torso), () => _currentHero.Equipment.torso },
        { (ItemType.Armor, ItemCategory.Gloves), () => _currentHero.Equipment.gloves },
        { (ItemType.Armor, ItemCategory.Pants), () => _currentHero.Equipment.pants },
        { (ItemType.Armor, ItemCategory.Boots), () => _currentHero.Equipment.boots }
    };

    private static readonly Dictionary<(ItemType, ItemCategory), System.Action<InventoryItem>> _equipmentSetters = new()
    {
        { (ItemType.Weapon, ItemCategory.None), item => _currentHero.Equipment.weapon = item },
        { (ItemType.Armor, ItemCategory.Helmet), item => _currentHero.Equipment.helmet = item },
        { (ItemType.Armor, ItemCategory.Torso), item => _currentHero.Equipment.torso = item },
        { (ItemType.Armor, ItemCategory.Gloves), item => _currentHero.Equipment.gloves = item },
        { (ItemType.Armor, ItemCategory.Pants), item => _currentHero.Equipment.pants = item },
        { (ItemType.Armor, ItemCategory.Boots), item => _currentHero.Equipment.boots = item },
    };

    #endregion

    #region Armor Compatibility System

    /// <summary>
    /// Tabla de compatibilidad entre armas y tipos de armadura.
    /// Define qué tipos de armadura son compatibles con cada categoría de arma.
    /// </summary>
    private static readonly Dictionary<ItemCategory, ArmorType[]> WeaponArmorCompatibility = new()
    {
        { ItemCategory.SwordAndShield, new[] { ArmorType.Heavy, ArmorType.Medium, ArmorType.Light } },
        { ItemCategory.TwoHandedSword, new[] { ArmorType.Medium, ArmorType.Light } },
        { ItemCategory.Bow, new[] { ArmorType.Light } },
        { ItemCategory.Spear, new[] { ArmorType.Medium, ArmorType.Light } }
    };

    #endregion

    /// <summary>
    /// Tipos de conflicto de compatibilidad de equipamiento.
    /// </summary>

    /// <summary>
    /// Inicializa el servicio con el héroe activo.
    /// </summary>
    public static void Initialize(HeroData hero)
    {
        if (hero == null) throw new ArgumentNullException(nameof(hero));
        _currentHero = (IHeroInventoryMutator)hero;

        if (_currentHero.Equipment == null)
        {
            _currentHero.Equipment = new Equipment();
            Log("Initialized empty equipment for hero", LogType.Info);
        }

        Log($"Equipment manager initialized for hero: {hero.heroName}", LogType.Info);
    }

    #region Equipment Operations

    /// <summary>
    /// Equipa un item del inventario. Maneja intercambio automático si ya hay algo equipado.
    /// Incluye validaciones de compatibilidad de armadura y confirmaciones de usuario.
    /// </summary>
    public static bool EquipItem(InventoryItem item)
        => InventoryEventService.RunTransaction(() => EquipItemCore(item));

    public static EquipmentCompatibilityInfo GetCompatibility(InventoryItem item)
    {
        if (!CanEquipItem(item) || !InventoryStorageService.Contains(item))
            return new EquipmentCompatibilityInfo(false, hasInventorySpace: false);
        return CheckEquipmentCompatibility(item, InventoryUtils.GetItemData(item.itemId));
    }

    private static bool EquipItemCore(InventoryItem item)
    {
        if (!ValidateEquipOperation(item))
        {
            Log($"Invalid equip operation for item: {item?.itemId}", LogType.Warning);
            return false;
        }

        ItemDataSO itemData = InventoryUtils.GetItemData(item.itemId);
        if (itemData == null)
        {
            Log($"Item data not found for: {item.itemId}", LogType.Error);
            return false;
        }

        var compatibility = CheckEquipmentCompatibility(item, itemData);
        if (!compatibility.HasInventorySpace) return false;
        var currentlyEquipped = GetEquippedItem(itemData.itemType, itemData.itemCategory);

        int originalSlot = item.slotIndex;
        if (!InventoryStorageService.RemoveSpecificItem(item))
        {
            Log($"Failed to remove {item.itemId} from inventory slot {originalSlot}", LogType.Error);
            return false;
        }

        // PASO 2: Obtener el item actualmente equipado en este slot
        if (!UnequipIncompatiblePieces(compatibility.IncompatiblePieces)) return false;

        // PASO 3: Equipar el nuevo item
        if (!SetEquippedItem(itemData.itemType, itemData.itemCategory, item))
        {
            // Si falla, devolver el item al inventario para mantener consistencia
            InventoryStorageService.AddExistingItemAtSlot(item, originalSlot);
            Log($"Failed to equip item in slot: {itemData.itemType}. Item returned to inventory.", LogType.Error);
            return false;
        }

        // PASO 4: Si había algo equipado, colocarlo en el slot liberado
        if (currentlyEquipped != null && currentlyEquipped.itemId != "")
        {
            if (!InventoryStorageService.AddExistingItemAtSlot(currentlyEquipped, originalSlot))
            {
                // Si no se puede colocar en el slot específico, buscar cualquier slot disponible
                if (!InventoryStorageService.AddExistingItem(currentlyEquipped))
                {
                    // Si falla completamente, revertir el equipamiento
                    SetEquippedItem(itemData.itemType, itemData.itemCategory, currentlyEquipped);
                    InventoryStorageService.AddExistingItemAtSlot(item, originalSlot);
                    Log($"Failed to place {currentlyEquipped.itemId} in inventory. Equipment reverted.", LogType.Error);
                    return false;
                }
                Log($"Could not place {currentlyEquipped.itemId} in original slot {originalSlot}, placed in available slot instead", LogType.Warning);
            }
            Log($"Swapped {item.itemId} (equipped) with {currentlyEquipped.itemId} (to slot {originalSlot})", LogType.Info);
            InventoryEventService.TriggerItemEquipped(item, currentlyEquipped);
        }
        else
        {
            Log($"Equipped {item.itemId} from slot {originalSlot} in empty {itemData.itemType} slot", LogType.Info);
            InventoryEventService.TriggerItemEquipped(item);
        }
        
        return true;
    }

    /// <summary>
    /// Desequipa un item de un slot específico y lo devuelve al inventario.
    /// </summary>
    public static bool UnequipItem(ItemType itemType, ItemCategory itemCategory)
        => InventoryEventService.RunTransaction(() => UnequipItemCore(itemType, itemCategory));

    private static bool UnequipItemCore(ItemType itemType, ItemCategory itemCategory)
    {
        var equippedItem = GetEquippedItem(itemType, itemCategory);
        if (equippedItem == null)
        {
            Log($"No item equipped in slot: {itemType}", LogType.Warning);
            return false;
        }

        if (!PerformUnequip(equippedItem, itemType, itemCategory))
        {
            return false;
        }

        InventoryEventService.TriggerItemUnequipped(equippedItem);
        return true;
    }

    /// <summary>
    /// Intercambia directamente dos items de equipment sin pasar por el inventario.
    /// </summary>
    public static bool SwapEquipment(ItemType slotType1, ItemCategory itemCategory1, ItemType slotType2, ItemCategory itemCategory2)
        => InventoryEventService.RunTransaction(() => SwapEquipmentCore(slotType1, itemCategory1, slotType2, itemCategory2));

    private static bool SwapEquipmentCore(ItemType slotType1, ItemCategory itemCategory1, ItemType slotType2, ItemCategory itemCategory2)
    {
        var item1 = GetEquippedItem(slotType1, itemCategory1);
        var item2 = GetEquippedItem(slotType2, itemCategory2);

        if (item1 == null && item2 == null)
        {
            Log("Cannot swap - both slots are empty", LogType.Warning);
            return false;
        }

        // Verificar que los items pueden equiparse en los slots destino
        if (item1 != null && !CanEquipInSlot(item1, slotType2, itemCategory2))
        {
            Log($"{item1.itemId} cannot be equipped in {slotType2} slot", LogType.Warning);
            return false;
        }

        if (item2 != null && !CanEquipInSlot(item2, slotType1, itemCategory1))
        {
            Log($"{item2.itemId} cannot be equipped in {slotType1} slot", LogType.Warning);
            return false;
        }

        // Realizar el intercambio
        SetEquippedItem(slotType1, itemCategory1, item2);
        SetEquippedItem(slotType2, itemCategory2, item1);

        string swapInfo = $"Swapped equipment: {slotType1} <-> {slotType2}";
        Log(swapInfo, LogType.Info);
        InventoryEventService.LogInventoryOperation(swapInfo);
        InventoryEventService.TriggerInventoryChanged();
        if (item1 != null) InventoryEventService.TriggerItemEquipped(item1);
        if (item2 != null) InventoryEventService.TriggerItemEquipped(item2);

        return true;
    }

    #endregion

    #region Equipment Queries

    /// <summary>
    /// Obtiene el item equipado en un slot específico.
    /// </summary>
    public static InventoryItem GetEquippedItem(ItemType itemType, ItemCategory itemCategory)
    {
        if (_currentHero?.Equipment == null) return null;
        itemCategory = itemType == ItemType.Weapon ? ItemCategory.None : itemCategory;
        var key = (itemType, itemCategory);
        return _equipmentGetters.TryGetValue(key, out var getter) ? getter() : null;
    }

    /// <summary>
    /// Obtiene todos los items equipados como array.
    /// </summary>
    public static InventoryItem[] GetAllEquippedItems()
    {
        if (_currentHero?.Equipment == null) return new InventoryItem[0];

        return new InventoryItem[]
        {
            _currentHero.Equipment.weapon,
            _currentHero.Equipment.helmet,
            _currentHero.Equipment.torso,
            _currentHero.Equipment.gloves,
            _currentHero.Equipment.pants,
            _currentHero.Equipment.boots
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

    private static bool IsItemEquippable(InventoryItem item, out ItemDataSO itemData)
    {
        itemData = null;
        if (item == null || !item.IsEquipment)
        {
            Log("Cannot equip null or non-equipment item", LogType.Warning);
            return false;
        }
        itemData = InventoryUtils.GetItemData(item.itemId);
        if (itemData == null || !InventoryUtils.IsEquippableType(itemData.itemType))
        {
            Log("Item data not found or not equippable", LogType.Warning);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Verifica si un item puede ser equipado.
    /// </summary>
    public static bool CanEquipItem(InventoryItem item)
    {
        ItemDataSO itemData;
        return IsItemEquippable(item, out itemData);
    }

    /// <summary>
    /// Verifica si un item puede equiparse en un slot específico.
    /// </summary>
    public static bool CanEquipInSlot(InventoryItem item, ItemType slotType)
    {
        if (!CanEquipItem(item)) return false;

        ItemDataSO itemData = InventoryUtils.GetItemData(item.itemId);
        if (itemData == null) return false;

        return itemData.itemType == slotType;
    }

    public static bool CanEquipInSlot(InventoryItem item, ItemType slotType, ItemCategory category)
    {
        if (!CanEquipInSlot(item, slotType)) return false;
        return slotType == ItemType.Weapon || InventoryUtils.GetItemData(item.itemId).itemCategory == category;
    }

    /// <summary>
    /// Calcula el total de stats proporcionados por todo el equipment equipado.
    /// </summary>
    public static Dictionary<string, float> CalculateTotalEquipmentStats()
        => CalculateTotalEquipmentStats(_currentHero);

    public static Dictionary<string, float> CalculateTotalEquipmentStats(IHeroInventory hero)
        => CalculateTotalEquipmentStats(hero?.Equipment);

    /// <summary>Sums the generated stats of the equipped items (any hero: local, remote or AI).</summary>
    public static Dictionary<string, float> CalculateTotalEquipmentStats(Equipment equipment)
    {
        var totalStats = new Dictionary<string, float>();
        if (equipment == null) return totalStats;
        var equippedItems = new[] { equipment.weapon, equipment.helmet, equipment.torso,
            equipment.gloves, equipment.pants, equipment.boots };

        if (equippedItems.Length == 0) Debug.LogWarning("[EquipmentManager] No equipped items found for stat calculation");
        
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

    #region Armor Compatibility Methods

    /// <summary>
    /// Verifica la compatibilidad del equipamiento y determina si se requiere confirmación.
    /// </summary>
    private static EquipmentCompatibilityInfo CheckEquipmentCompatibility(InventoryItem item, ItemDataSO itemData)
    {
        if (itemData.itemType == ItemType.Weapon)
        {
            return CheckCompatibility(item, itemData, true);
        }
        else if (itemData.itemType == ItemType.Armor)
        {
            return CheckCompatibility(item, itemData, false);
        }

        // No hay conflictos para otros tipos de items
        return new EquipmentCompatibilityInfo(false);
    }

    private static EquipmentCompatibilityInfo CheckCompatibility(InventoryItem item, ItemDataSO itemData, bool isWeapon)
    {
        List<InventoryItem> incompatibleItems = isWeapon 
            ? GetIncompatibleArmorPieces(itemData.itemCategory) 
            : new List<InventoryItem> { GetIncompatibleWeapon(itemData.armorType) }.Where(x => x != null).ToList();
        
        var previous = GetEquippedItem(itemData.itemType, itemData.itemCategory);
        int returnedCount = incompatibleItems.Count + (previous != null && !incompatibleItems.Contains(previous) ? 1 : 0);
        bool hasSpace = InventoryStorageService.GetAllItems().Count - 1 + returnedCount <= InventoryStorageService.InventoryLimit;
        if (incompatibleItems.Count == 0) return new EquipmentCompatibilityInfo(false, hasInventorySpace: hasSpace);
        string warningMessage = BuildCompatibilityWarningMessage(itemData, incompatibleItems, isWeapon, hasSpace);
        
        return new EquipmentCompatibilityInfo(true, incompatibleItems, hasSpace, warningMessage, 
            isWeapon ? CompatibilityConflictType.WeaponIncompatibleWithArmor : CompatibilityConflictType.ArmorIncompatibleWithWeapon);
    }

    private static string BuildCompatibilityWarningMessage(ItemDataSO itemData, List<InventoryItem> incompatibleItems, bool isWeapon, bool hasSpace)
    {
        string itemTypeName = isWeapon ? GetWeaponTypeName(itemData.itemCategory) : GetArmorTypeName(itemData.armorType);
        string compatibleTypes = isWeapon 
            ? string.Join(", ", WeaponArmorCompatibility[itemData.itemCategory].Select(a => GetArmorTypeName(a)))
            : "compatible weapons";
        
        string message = hasSpace 
            ? $"Equipar {itemData.name} desequipará {incompatibleItems.Count} piezas incompatibles."
            : $"No hay espacio suficiente en el inventario para desequipar las piezas incompatibles.";
        
        if (isWeapon && incompatibleItems.Count > 0)
        {
            message += $"\n\nLa {itemTypeName} '{itemData.name}' solo es compatible con armadura: {compatibleTypes}.";
        }
        
        return message;
    }

    /// <summary>
    /// Verifica si un arma es compatible con un tipo de armadura.
    /// </summary>
    private static bool IsWeaponCompatibleWithArmor(ItemCategory weaponCategory, ArmorType armorType)
    {
        if (!WeaponArmorCompatibility.ContainsKey(weaponCategory))
        {
            return true; // Si no hay restricciones definidas, es compatible
        }

        var compatibleArmorTypes = WeaponArmorCompatibility[weaponCategory];
        return compatibleArmorTypes.Contains(armorType);
    }

    /// <summary>
    /// Obtiene las piezas de armadura incompatibles con una categoría de arma.
    /// </summary>
    private static List<InventoryItem> GetIncompatibleArmorPieces(ItemCategory weaponCategory)
    {
        var incompatiblePieces = new List<InventoryItem>();
        var equippedItems = GetAllEquippedItems();

        foreach (var equippedItem in equippedItems)
        {
            if (equippedItem == null || !equippedItem.IsEquipment) continue;

            var equippedItemData = InventoryUtils.GetItemData(equippedItem.itemId);
            if (equippedItemData?.itemType != ItemType.Armor) continue;

            if (!IsWeaponCompatibleWithArmor(weaponCategory, equippedItemData.armorType))
            {
                incompatiblePieces.Add(equippedItem);
            }
        }

        return incompatiblePieces;
    }

    /// <summary>
    /// Obtiene el arma incompatible con un tipo de armadura.
    /// </summary>
    private static InventoryItem GetIncompatibleWeapon(ArmorType armorType)
    {
        var equippedWeapon = _currentHero?.Equipment?.weapon;
        if (equippedWeapon == null || !equippedWeapon.IsEquipment) return null;

        var weaponData = InventoryUtils.GetItemData(equippedWeapon.itemId);
        if (weaponData?.itemType != ItemType.Weapon) return null;

        return !IsWeaponCompatibleWithArmor(weaponData.itemCategory, armorType) 
            ? equippedWeapon 
            : null;
    }

    /// <summary>
    /// Desequipa múltiples piezas incompatibles y las coloca en el inventario.
    /// </summary>
    private static bool UnequipIncompatiblePieces(List<InventoryItem> incompatiblePieces)
    {
        if (incompatiblePieces == null || incompatiblePieces.Count == 0)
        {
            return true;
        }

        Log($"[EquipmentManager] Unequipping {incompatiblePieces.Count} incompatible pieces...", LogType.Info);

        foreach (var piece in incompatiblePieces)
        {
            var pieceData = InventoryUtils.GetItemData(piece.itemId);
            if (pieceData == null) continue;

            if (!PerformUnequip(piece, pieceData.itemType, pieceData.itemCategory))
            {
                Log($"[EquipmentManager] Failed to unequip incompatible piece: {piece.itemId}", LogType.Error);
                return false;
            }

            Log($"[EquipmentManager] Successfully unequipped incompatible piece: {pieceData.name}", LogType.Info);
            InventoryEventService.TriggerItemUnequipped(piece);
        }

        return true;
    }

    #endregion

    #region Private Helper Methods

    private static bool PerformUnequip(InventoryItem item, ItemType itemType, ItemCategory itemCategory)
    {
        if (!InventoryStorageService.HasSpace())
        {
            Log("No space in inventory for unequip", LogType.Warning);
            return false;
        }
        
        if (!SetEquippedItem(itemType, itemCategory, null))
        {
            Log("Failed to unequip item", LogType.Error);
            return false;
        }
        
        if (!InventoryStorageService.AddExistingItem(item))
        {
            // Revertir
            SetEquippedItem(itemType, itemCategory, item);
            Log("Failed to add to inventory, unequip reverted", LogType.Error);
            return false;
        }
        
        Log($"Unequipped {item.itemId}", LogType.Info);
        return true;
    }

    private static bool ValidateEquipOperation(InventoryItem item)
    {
        if (_currentHero?.Equipment == null)
        {
            Log("Equipment not initialized", LogType.Error);
            return false;
        }

        if (!CanEquipItem(item)) return false;

        // Verificar que el item está en el inventario
        bool itemInInventory = InventoryStorageService.Contains(item);

        if (!itemInInventory)
        {
            Log($"Item {item.itemId} not found in inventory", LogType.Warning);
            return false;
        }

        return true;
    }

    private static bool SetEquippedItem(ItemType itemType, ItemCategory itemCategory, InventoryItem item)
    {
        if (_currentHero?.Equipment == null) return false;
        if(item != null) item.slotIndex = -1; //Lo sacamos del inventario
        itemCategory = itemType == ItemType.Weapon ? ItemCategory.None : itemCategory;
        var key = (itemType, itemCategory);
        if (_equipmentSetters.TryGetValue(key, out var setter))
        {
            setter(item);
            return true;
        }
        Log($"Invalid equipment slot {itemType}, {itemCategory}", LogType.Error);
        return false;
    }

    #endregion

    #region Armor Compatibility Implementation

    /// <summary>
    /// Obtiene el nombre legible del tipo de arma.
    /// </summary>
    private static string GetWeaponTypeName(ItemCategory weaponCategory)
    {
        return weaponCategory switch
        {
            ItemCategory.SwordAndShield => "espada y escudo",
            ItemCategory.TwoHandedSword => "espada a dos manos",
            ItemCategory.Bow => "arco",
            ItemCategory.Spear => "lanza",
            _ => weaponCategory.ToString().ToLower()
        };
    }

    /// <summary>
    /// Obtiene el nombre legible del tipo de armadura.
    /// </summary>
    private static string GetArmorTypeName(ArmorType armorType)
    {
        return armorType switch
        {
            ArmorType.Heavy => "pesada",
            ArmorType.Medium => "mediana",
            ArmorType.Light => "ligera",
            _ => armorType.ToString().ToLower()
        };
    }

    #endregion

    #region Logging

    private enum LogType { Info, Warning, Error }

    private static void Log(string message, LogType logType = LogType.Info)
    {
        string prefix = "[EquipmentManagerService]";
        switch (logType)
        {
            case LogType.Info: Debug.Log($"{prefix} {message}"); break;
            case LogType.Warning: Debug.LogWarning($"{prefix} {message}"); break;
            case LogType.Error: Debug.LogError($"{prefix} {message}"); break;
        }
    }

    #endregion
}
