using System;
using System.Linq;
using UnityEngine;

/// <summary>
/// Servicio especializado en manejo de eventos y persistencia del inventario.
/// Coordina notificaciones, logging y guardado automático de datos.
/// </summary>
public static class InventoryEventService
{
    private static HeroData _currentHero;

    // Eventos del inventario
    public static event Action OnInventoryChanged;
    public static event Action<InventoryItem> OnItemAdded;
    public static event Action<InventoryItem> OnItemRemoved;
    public static event Action<InventoryItem, InventoryItem> OnItemEquipped; // (equipped, unequipped)
    public static event Action<InventoryItem> OnItemUnequipped;
    public static event Action<InventoryItem> OnItemUsed;

    /// <summary>
    /// Inicializa el servicio con el héroe activo.
    /// </summary>
    public static void Initialize(HeroData hero)
    {
        _currentHero = hero ?? throw new ArgumentNullException(nameof(hero));
        LogInfo($"Event service initialized for hero: {_currentHero.heroName}");
    }

    #region Event Triggers

    /// <summary>
    /// Dispara el evento de cambio general del inventario y guarda automáticamente.
    /// </summary>
    public static void TriggerInventoryChanged()
    {
        OnInventoryChanged?.Invoke();
        AutoSaveHeroData();
        LogInfo("Inventory changed event triggered");
    }

    /// <summary>
    /// Dispara el evento de item agregado.
    /// </summary>
    public static void TriggerItemAdded(InventoryItem item)
    {
        if (item == null) return;
        
        OnItemAdded?.Invoke(item);
        TriggerInventoryChanged();
        LogInventoryOperation($"Item added: {item.itemId} (Quantity: {item.quantity})");
    }

    /// <summary>
    /// Dispara el evento de item removido.
    /// </summary>
    public static void TriggerItemRemoved(InventoryItem item)
    {
        if (item == null) return;
        
        OnItemRemoved?.Invoke(item);
        TriggerInventoryChanged();
        LogInventoryOperation($"Item removed: {item.itemId} (Quantity: {item.quantity})");
    }

    /// <summary>
    /// Dispara el evento de item equipado.
    /// </summary>
    public static void TriggerItemEquipped(InventoryItem equippedItem, InventoryItem unequippedItem = null)
    {
        OnItemEquipped?.Invoke(equippedItem, unequippedItem);
        TriggerInventoryChanged();
        
        string message = $"Item equipped: {equippedItem.itemId}";
        if (unequippedItem != null)
        {
            message += $" (replaced: {unequippedItem.itemId})";
        }
        LogInventoryOperation(message);
    }

    /// <summary>
    /// Dispara el evento de item desequipado.
    /// </summary>
    public static void TriggerItemUnequipped(InventoryItem item)
    {
        if (item == null) return;
        
        OnItemUnequipped?.Invoke(item);
        TriggerInventoryChanged();
        LogInventoryOperation($"Item unequipped: {item.itemId}");
    }

    /// <summary>
    /// Dispara el evento de item usado/consumido.
    /// </summary>
    public static void TriggerItemUsed(InventoryItem item)
    {
        if (item == null) return;
        
        OnItemUsed?.Invoke(item);
        TriggerInventoryChanged();
        LogInventoryOperation($"Item used: {item.itemId}");
    }

    #endregion

    #region Persistence Management

    /// <summary>
    /// Guarda automáticamente los datos del héroe usando el sistema de persistencia existente.
    /// </summary>
    public static void AutoSaveHeroData()
    {
        if (_currentHero == null)
        {
            LogWarning("Cannot save - no current hero set");
            return;
        }

        try
        {
            // Conectar con el sistema de guardado existente
            if (PlayerSessionService.CurrentPlayer != null)
            {
                SaveSystem.SavePlayer(PlayerSessionService.CurrentPlayer);
                LogInfo("Hero data auto-saved successfully");
            }
            else
            {
                LogWarning("Cannot save - no current player in session");
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to auto-save hero data: {ex.Message}");
        }
    }

    /// <summary>
    /// Fuerza el guardado manual de los datos del héroe.
    /// </summary>
    public static void ForceSaveHeroData()
    {
        LogInfo("Force saving hero data...");
        AutoSaveHeroData();
    }

    #endregion

    #region Validation and Integrity

    /// <summary>
    /// Valida la integridad completa del inventario y equipment.
    /// </summary>
    public static bool ValidateInventoryIntegrity()
    {
        if (_currentHero?.inventory == null)
        {
            LogError("Cannot validate - inventory is null");
            return false;
        }

        bool isValid = true;
        int validationErrors = 0;

        // Validar items nulos
        foreach (var item in _currentHero.inventory)
        {
            if (item == null)
            {
                LogWarning("Found null item in inventory");
                validationErrors++;
                isValid = false;
                continue;
            }

            // Validar datos básicos del item
            if (string.IsNullOrEmpty(item.itemId))
            {
                LogWarning($"Item with empty ID found at slot {item.slotIndex}");
                validationErrors++;
                isValid = false;
            }

            if (item.quantity <= 0)
            {
                LogWarning($"Item {item.itemId} has invalid quantity: {item.quantity}");
                validationErrors++;
                isValid = false;
            }

            // Validar equipment específico
            if (item.IsEquipment)
            {
                if (string.IsNullOrEmpty(item.instanceId))
                {
                    LogWarning($"Equipment item {item.itemId} missing instanceId");
                    validationErrors++;
                    isValid = false;
                }

                if (item.quantity != 1)
                {
                    LogWarning($"Equipment item {item.itemId} has invalid quantity: {item.quantity}");
                    validationErrors++;
                    isValid = false;
                }
            }
        }

        // Validar equipment equipado
        if (_currentHero.equipment != null)
        {
            ValidateEquippedItems();
        }

        string validationResult = isValid ? "PASSED" : $"FAILED ({validationErrors} errors)";
        LogInfo($"Inventory integrity validation: {validationResult}");
        
        return isValid;
    }

    /// <summary>
    /// Valida que los items equipados existan en el inventario.
    /// </summary>
    private static void ValidateEquippedItems()
    {
        var equipment = _currentHero.equipment;
        var inventory = _currentHero.inventory;

        // Validar cada slot de equipment
        ValidateEquippedItem(equipment.weapon, "weapon", inventory);
        ValidateEquippedItem(equipment.helmet, "helmet", inventory);
        ValidateEquippedItem(equipment.torso, "torso", inventory);
        ValidateEquippedItem(equipment.gloves, "gloves", inventory);
        ValidateEquippedItem(equipment.pants, "pants", inventory);
        ValidateEquippedItem(equipment.boots, "boots", inventory);
    }

    /// <summary>
    /// Valida un item equipado específico.
    /// </summary>
    private static void ValidateEquippedItem(InventoryItem equippedItem, string slotName, System.Collections.Generic.List<InventoryItem> inventory)
    {
        if (equippedItem == null) return;

        bool foundInInventory = inventory.Any(item => 
            item.instanceId == equippedItem.instanceId);

        if (!foundInInventory)
        {
            LogWarning($"Equipped {slotName} ({equippedItem.itemId}) not found in inventory");
        }
    }

    #endregion

    #region Logging and Operations Tracking

    /// <summary>
    /// Registra una operación específica del inventario con timestamp.
    /// </summary>
    public static void LogInventoryOperation(string operation)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        LogInfo($"[{timestamp}] {operation}");
    }

    /// <summary>
    /// Genera un reporte detallado del estado actual del inventario.
    /// </summary>
    public static string GenerateInventoryReport()
    {
        if (_currentHero?.inventory == null)
        {
            return "Inventory not initialized";
        }

        var report = new System.Text.StringBuilder();
        report.AppendLine($"=== INVENTORY REPORT - {_currentHero.heroName} ===");
        report.AppendLine($"Total Items: {_currentHero.inventory.Count}/{InventoryStorageService.InventoryLimit}");
        report.AppendLine($"Available Slots: {InventoryStorageService.InventoryLimit - _currentHero.inventory.Count}");
        report.AppendLine();

        // Groupar items por tipo
        var itemsByType = _currentHero.inventory.GroupBy(item => item.itemType);
        foreach (var group in itemsByType)
        {
            report.AppendLine($"{group.Key}: {group.Count()} items");
            foreach (var item in group)
            {
                string instanceInfo = item.IsEquipment ? $" (ID: {item.instanceId})" : "";
                report.AppendLine($"  - {item.itemId} x{item.quantity} [Slot {item.slotIndex}]{instanceInfo}");
            }
            report.AppendLine();
        }

        // Equipment status
        if (_currentHero.equipment != null)
        {
            report.AppendLine("=== EQUIPPED ITEMS ===");
            AppendEquipmentInfo(report, "Weapon", _currentHero.equipment.weapon);
            AppendEquipmentInfo(report, "Helmet", _currentHero.equipment.helmet);
            AppendEquipmentInfo(report, "Torso", _currentHero.equipment.torso);
            AppendEquipmentInfo(report, "Gloves", _currentHero.equipment.gloves);
            AppendEquipmentInfo(report, "Pants", _currentHero.equipment.pants);
            AppendEquipmentInfo(report, "Boots", _currentHero.equipment.boots);
        }

        return report.ToString();
    }

    private static void AppendEquipmentInfo(System.Text.StringBuilder report, string slotName, InventoryItem item)
    {
        if (item != null)
        {
            report.AppendLine($"{slotName}: {item.itemId} (ID: {item.instanceId})");
        }
        else
        {
            report.AppendLine($"{slotName}: Empty");
        }
    }

    #endregion

    #region Event Cleanup

    /// <summary>
    /// Limpia todos los event listeners. Útil al cambiar de héroe o cerrar el juego.
    /// </summary>
    public static void ClearAllEventListeners()
    {
        OnInventoryChanged = null;
        OnItemAdded = null;
        OnItemRemoved = null;
        OnItemEquipped = null;
        OnItemUnequipped = null;
        OnItemUsed = null;
        
        LogInfo("All event listeners cleared");
    }

    #endregion

    #region Logging

    private static void LogInfo(string message)
    {
        Debug.Log($"[InventoryEventService] {message}");
    }

    private static void LogWarning(string message)
    {
        Debug.LogWarning($"[InventoryEventService] {message}");
    }

    private static void LogError(string message)
    {
        Debug.LogError($"[InventoryEventService] {message}");
    }

    #endregion
}
