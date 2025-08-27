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
    private static HeroData _currentHero;

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
    public enum CompatibilityConflictType
    {
        None,
        WeaponIncompatibleWithArmor,
        ArmorIncompatibleWithWeapon
    }

    /// <summary>
    /// Estructura de datos que contiene información sobre la compatibilidad de equipamiento.
    /// </summary>
    [Serializable]
    public struct EquipmentCompatibilityInfo
    {
        public bool RequiresConfirmation;
        public string[] IncompatiblePieceNames;
        public List<InventoryItem> IncompatiblePieces;
        public bool HasInventorySpace;
        public string WarningMessage;
        public CompatibilityConflictType ConflictType;

        public EquipmentCompatibilityInfo(
            bool requiresConfirmation,
            List<InventoryItem> incompatiblePieces = null,
            bool hasInventorySpace = true,
            string warningMessage = "",
            CompatibilityConflictType conflictType = CompatibilityConflictType.None)
        {
            RequiresConfirmation = requiresConfirmation;
            IncompatiblePieces = incompatiblePieces ?? new List<InventoryItem>();
            HasInventorySpace = hasInventorySpace;
            WarningMessage = warningMessage;
            ConflictType = conflictType;

            // Generar nombres de piezas incompatibles
            if (IncompatiblePieces != null && IncompatiblePieces.Count > 0)
            {
                IncompatiblePieceNames = new string[IncompatiblePieces.Count];
                for (int i = 0; i < IncompatiblePieces.Count; i++)
                {
                    var itemData = InventoryUtils.GetItemData(IncompatiblePieces[i].itemId);
                    IncompatiblePieceNames[i] = itemData?.name ?? IncompatiblePieces[i].itemId;
                }
            }
            else
            {
                IncompatiblePieceNames = new string[0];
            }
        }
    }

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
    /// Incluye validaciones de compatibilidad de armadura y confirmaciones de usuario.
    /// </summary>
    public static bool EquipItem(InventoryItem item)
    {
        if (!ValidateEquipOperation(item))
        {
            LogError($"[EquipmentManager]Invalid equip operation for item: {item.itemId}");
            return false;
        }

        var itemData = InventoryUtils.GetItemData(item.itemId);
        if (itemData == null)
        {
            LogError($"[EquipmentManager]Item data not found for: {item.itemId}");
            return false;
        }

       executeCompatibilityValidations(item, itemData);

        int originalSlot = item.slotIndex;
        if (!InventoryStorageService.RemoveSpecificItem(item))
        {
            LogError($"[EquipmentManager]Failed to remove {item.itemId} from inventory slot {originalSlot}");
            return false;
        }

        // PASO 2: Obtener el item actualmente equipado en este slot
        var currentlyEquipped = GetEquippedItem(itemData.itemType, itemData.itemCategory);

        // PASO 3: Equipar el nuevo item
        if (!SetEquippedItem(itemData.itemType, itemData.itemCategory, item))
        {
            // Si falla, devolver el item al inventario para mantener consistencia
            InventoryStorageService.AddExistingItemAtSlot(item, originalSlot);
            LogError($"[EquipmentManager]Failed to equip item in slot: {itemData.itemType}. Item returned to inventory.");
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

    private static bool executeCompatibilityValidations(InventoryItem item, ItemData itemData)
    {
        var compatibilityInfo = CheckEquipmentCompatibility(item, itemData);
        if (compatibilityInfo.RequiresConfirmation)
        {
            if (!compatibilityInfo.HasInventorySpace)
            {
                LogWarning($"Cannot equip {item.itemId}: {compatibilityInfo.WarningMessage}");
                return false;
            }

            // TODO: Integrar con sistema UI para mostrar confirmación
            // Por ahora, proceder automáticamente (para testing)
            LogInfo($"Equipment compatibility warning: {compatibilityInfo.WarningMessage}");

            // Desequipar piezas incompatibles ANTES de continuar
            if (!UnequipIncompatiblePieces(compatibilityInfo.IncompatiblePieces))
            {
                LogError($"Failed to unequip incompatible pieces for {item.itemId}");
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Desequipa un item de un slot específico y lo devuelve al inventario.
    /// </summary>
    public static bool UnequipItem(ItemType itemType, ItemCategory itemCategory)
    {
        var equippedItem = GetEquippedItem(itemType, itemCategory);
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
        if (!SetEquippedItem(itemType, itemCategory, null))
        {
            LogError($"Failed to unequip item from slot: {itemType}");
            return false;
        }
        
        // Devolver al inventario
        if (!InventoryStorageService.AddExistingItem(equippedItem))
        {
            // Revertir si no se puede agregar al inventario
            SetEquippedItem(itemType, itemCategory, equippedItem);
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
    public static bool SwapEquipment(ItemType slotType1, ItemCategory itemCategory1, ItemType slotType2, ItemCategory itemCategory2)
    {
        var item1 = GetEquippedItem(slotType1, itemCategory1);
        var item2 = GetEquippedItem(slotType2, itemCategory2);

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
        SetEquippedItem(slotType1, itemCategory1, item2);
        SetEquippedItem(slotType2, itemCategory2, item1);

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
    public static InventoryItem GetEquippedItem(ItemType itemType, ItemCategory itemCategory)
    {
        if (_currentHero?.equipment == null) return null;
        if (itemType == ItemType.Armor)
        {
            return itemCategory switch
            {
                ItemCategory.Helmet => _currentHero.equipment.helmet,
                ItemCategory.Torso => _currentHero.equipment.torso,
                ItemCategory.Gloves => _currentHero.equipment.gloves,
                ItemCategory.Pants => _currentHero.equipment.pants,
                ItemCategory.Boots => _currentHero.equipment.boots,
                _ => null
            };
        }
        else if (itemType == ItemType.Weapon)
        {
            return _currentHero.equipment.weapon;
        }
        else
        {
            LogWarning($"[EquipmentManager]Invalid item type for equipment query: {itemType}");
            return null;
        }
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

    #region Armor Compatibility Methods

    /// <summary>
    /// Verifica la compatibilidad del equipamiento y determina si se requiere confirmación.
    /// </summary>
    private static EquipmentCompatibilityInfo CheckEquipmentCompatibility(InventoryItem item, ItemData itemData)
    {
        if (itemData.itemType == ItemType.Weapon)
        {
            return CheckWeaponCompatibility(item, itemData);
        }
        else if (itemData.itemType == ItemType.Armor)
        {
            return CheckArmorCompatibility(item, itemData);
        }

        // No hay conflictos para otros tipos de items
        return new EquipmentCompatibilityInfo(false);
    }

    /// <summary>
    /// Verifica la compatibilidad de un arma con la armadura equipada.
    /// </summary>
    private static EquipmentCompatibilityInfo CheckWeaponCompatibility(InventoryItem item, ItemData itemData)
    {
        var incompatiblePieces = GetIncompatibleArmorPieces(itemData.itemCategory);
        
        if (incompatiblePieces.Count == 0)
        {
            return new EquipmentCompatibilityInfo(false);
        }

        // Verificar espacio en inventario para las piezas incompatibles
        bool hasSpace = ValidateInventorySpaceForUnequip(incompatiblePieces);
        
        string warningMessage = hasSpace 
            ? $"Equipar {itemData.name} desequipará {incompatiblePieces.Count} piezas de armadura incompatibles."
            : $"No hay espacio suficiente en el inventario para desequipar las piezas incompatibles.";

        return new EquipmentCompatibilityInfo(
            requiresConfirmation: true,
            incompatiblePieces: incompatiblePieces,
            hasInventorySpace: hasSpace,
            warningMessage: warningMessage,
            conflictType: CompatibilityConflictType.WeaponIncompatibleWithArmor
        );
    }

    /// <summary>
    /// Verifica la compatibilidad de una armadura con el arma equipada.
    /// </summary>
    private static EquipmentCompatibilityInfo CheckArmorCompatibility(InventoryItem item, ItemData itemData)
    {
        var incompatibleWeapon = GetIncompatibleWeapon(itemData.armorType);
        
        if (incompatibleWeapon == null)
        {
            return new EquipmentCompatibilityInfo(false);
        }

        // Verificar espacio en inventario para el arma incompatible
        var incompatibleList = new List<InventoryItem> { incompatibleWeapon };
        bool hasSpace = ValidateInventorySpaceForUnequip(incompatibleList);

        string warningMessage = hasSpace 
            ? $"Equipar {itemData.name} desequipará el arma incompatible."
            : $"No hay espacio suficiente en el inventario para desequipar el arma incompatible.";

        return new EquipmentCompatibilityInfo(
            requiresConfirmation: true,
            incompatiblePieces: incompatibleList,
            hasInventorySpace: hasSpace,
            warningMessage: warningMessage,
            conflictType: CompatibilityConflictType.ArmorIncompatibleWithWeapon
        );
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
        var equippedWeapon = _currentHero?.equipment?.weapon;
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

        LogInfo($"[EquipmentManager] Unequipping {incompatiblePieces.Count} incompatible pieces...");

        foreach (var piece in incompatiblePieces)
        {
            var pieceData = InventoryUtils.GetItemData(piece.itemId);
            if (pieceData == null) continue;

            // Desequipar la pieza
            if (!SetEquippedItem(pieceData.itemType, pieceData.itemCategory, null))
            {
                LogError($"[EquipmentManager] Failed to unequip incompatible piece: {piece.itemId}");
                return false;
            }

            // Agregar al inventario
            if (!InventoryStorageService.AddExistingItem(piece))
            {
                LogError($"[EquipmentManager] Failed to add unequipped piece {piece.itemId} to inventory");
                // Intentar revertir el desequipamiento
                SetEquippedItem(pieceData.itemType, pieceData.itemCategory, piece);
                return false;
            }

            LogInfo($"[EquipmentManager] Successfully unequipped incompatible piece: {pieceData.name}");
            InventoryEventService.TriggerItemUnequipped(piece);
        }

        return true;
    }

    /// <summary>
    /// Valida si hay espacio suficiente en el inventario para desequipar las piezas especificadas.
    /// </summary>
    private static bool ValidateInventorySpaceForUnequip(List<InventoryItem> itemsToUnequip)
    {
        if (itemsToUnequip == null || itemsToUnequip.Count == 0)
        {
            return true;
        }

        // Simulamos que necesitamos al menos 1 slot por ítem
        // Para una validación más precisa, podríamos calcular el espacio exacto
        for (int i = 0; i < itemsToUnequip.Count; i++)
        {
            if (!InventoryStorageService.HasSpace())
            {
                LogWarning($"Not enough inventory space for unequipping {itemsToUnequip.Count} pieces");
                return false;
            }
        }

        return true;
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

    private static bool SetEquippedItem(ItemType itemType, ItemCategory itemCategory, InventoryItem item)
    {
        if (_currentHero?.equipment == null) return false;
        if(item != null) item.slotIndex = -1; //Lo sacamos del inventario
        switch (itemType)
        {
            case ItemType.Weapon:
                _currentHero.equipment.weapon = item;
                return true;
            case ItemType.Armor:
                switch (itemCategory)
                {
                    case ItemCategory.Helmet:
                        _currentHero.equipment.helmet = item;
                        return true;
                    case ItemCategory.Torso:
                        _currentHero.equipment.torso = item;
                        return true;
                    case ItemCategory.Gloves:
                        _currentHero.equipment.gloves = item;
                        return true;
                    case ItemCategory.Pants:
                        _currentHero.equipment.pants = item;
                        return true;
                    case ItemCategory.Boots:
                        _currentHero.equipment.boots = item;
                        return true;
                    default:
                        LogError($"Invalid armor category: {itemCategory}");
                        return false;
                }
            default:
                LogError($"Invalid equipment type: {itemType}");
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

    #region Armor Compatibility Implementation


    /// <summary>
    /// Verifica incompatibilidades cuando se equipa un arma.
    /// </summary>
    private static List<InventoryItem> CheckWeaponCompatibility(ItemData weaponData, out string warningMessage)
    {
        List<InventoryItem> incompatibleItems = new List<InventoryItem>();
        warningMessage = "";

        if (!WeaponArmorCompatibility.TryGetValue(weaponData.itemCategory, out ArmorType[] compatibleArmorTypes))
        {
            return incompatibleItems;
        }

        List<string> incompatiblePieceNames = new List<string>();

        // Verificar cada slot de armadura equipado
        var armorSlots = new[]
        {
            ItemCategory.Helmet,
            ItemCategory.Torso,
            ItemCategory.Gloves,
            ItemCategory.Pants,
            ItemCategory.Boots
        };

        foreach (var armorSlot in armorSlots)
        {
            var equippedArmor = GetEquippedItem(ItemType.Armor, armorSlot);
            if (equippedArmor != null)
            {
                var armorData = InventoryUtils.GetItemData(equippedArmor.itemId);
                if (armorData != null && !compatibleArmorTypes.Contains(armorData.armorType))
                {
                    incompatibleItems.Add(equippedArmor);
                    incompatiblePieceNames.Add(armorData.name);
                }
            }
        }

        if (incompatibleItems.Count > 0)
        {
            string weaponTypeName = GetWeaponTypeName(weaponData.itemCategory);
            string armorTypesText = string.Join(", ", compatibleArmorTypes.Select(a => GetArmorTypeName(a)));
            
            warningMessage = $"La {weaponTypeName} '{weaponData.name}' solo es compatible con armadura: {armorTypesText}.\n\n";
            warningMessage += $"Las siguientes piezas serán desequipadas:\n• {string.Join("\n• ", incompatiblePieceNames)}";
        }

        return incompatibleItems;
    }

    /// <summary>
    /// Verifica incompatibilidades cuando se equipa armadura.
    /// </summary>
    private static List<InventoryItem> CheckArmorCompatibility(ItemData armorData, out string warningMessage)
    {
        List<InventoryItem> incompatibleItems = new List<InventoryItem>();
        warningMessage = "";

        // Verificar si hay armas equipadas que sean incompatibles con este tipo de armadura
        var weaponSlots = new[]
        {
            (ItemType.Weapon, ItemCategory.SwordAndShield),
            (ItemType.Weapon, ItemCategory.TwoHandedSword),
            (ItemType.Weapon, ItemCategory.Bow),
            (ItemType.Weapon, ItemCategory.Spear)
        };

        List<string> incompatibleWeaponNames = new List<string>();

        foreach (var (weaponType, weaponCategory) in weaponSlots)
        {
            var equippedWeapon = GetEquippedItem(weaponType, weaponCategory);
            if (equippedWeapon != null)
            {
                // Verificar si esta arma es compatible con el tipo de armadura
                if (WeaponArmorCompatibility.TryGetValue(weaponCategory, out ArmorType[] compatibleArmorTypes))
                {
                    if (!compatibleArmorTypes.Contains(armorData.armorType))
                    {
                        incompatibleItems.Add(equippedWeapon);
                        var weaponData = InventoryUtils.GetItemData(equippedWeapon.itemId);
                        if (weaponData != null)
                        {
                            incompatibleWeaponNames.Add(weaponData.name);
                        }
                    }
                }
            }
        }

        if (incompatibleItems.Count > 0)
        {
            string armorTypeName = GetArmorTypeName(armorData.armorType);
            warningMessage = $"La armadura {armorTypeName} '{armorData.name}' no es compatible con las siguientes armas equipadas:\n\n";
            warningMessage += $"• {string.Join("\n• ", incompatibleWeaponNames)}\n\n";
            warningMessage += "Estas armas serán desequipadas.";
        }

        return incompatibleItems;
    }

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
