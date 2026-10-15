using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Servicio especializado en manejo de eventos y persistencia del inventario.
/// Coordina notificaciones, logging y guardado automático de datos.
/// </summary>
public static class InventoryEventService
{
    private static HeroData _currentHero;
    private static Action _saveAction;
    private static List<Action> _pendingEvents;
    private static bool _pendingChange;
    private static bool _pendingEquipmentChange;

    /// <summary>Roll back the complete operation; save and notify only its final state.</summary>
    public static bool RunTransaction(Func<bool> operation)
    {
        if (_currentHero == null || operation == null) return false;
        if (_pendingEvents != null) return operation();

        var snapshot = new InventorySnapshot(_currentHero);
        _pendingEvents = new List<Action>();
        _pendingChange = false;
        bool succeeded;
        try { succeeded = operation(); }
        catch (Exception ex)
        {
            LogWarning($"Operation reverted: {ex.Message}");
            succeeded = false;
        }
        var notifications = _pendingEvents;
        bool changed = _pendingChange;
        bool equipmentChanged = _pendingEquipmentChange;
        _pendingEquipmentChange = false;
        _pendingEvents = null;
        _pendingChange = false;
        if (!succeeded)
        {
            snapshot.Restore();
            return false;
        }
        if (changed)
        {
            AutoSaveHeroData();
            foreach (var notification in notifications) notification();
            if (equipmentChanged) Notify(OnEquipmentChanged);
            Notify(OnInventoryChanged);
        }
        return true;
    }

    public static void Reset()
    {
        _pendingEquipmentChange = false;
        _currentHero = null;
        _saveAction = null;
        _pendingEvents = null;
        _pendingChange = false;
    }

    /// <summary>Rebind views to a new session without saving an inventory operation.</summary>
    public static void NotifyContextChanged()
    {
        Notify(OnEquipmentChanged);
        Notify(OnInventoryChanged);
    }

    private static void Publish(Action notification, bool equipmentChanged = false)
    {
        if (_pendingEvents != null)
        {
            _pendingEvents.Add(notification);
            _pendingEquipmentChange |= equipmentChanged;
            _pendingChange = true;
        }
        else
        {
            AutoSaveHeroData();
            notification();
            if (equipmentChanged) Notify(OnEquipmentChanged);
            Notify(OnInventoryChanged);
        }
    }

    private static void Notify(Action listeners)
    {
        if (listeners == null) return;
        foreach (Action listener in listeners.GetInvocationList())
            try { listener(); }
            catch (Exception ex) { LogError($"Inventory observer failed: {ex.Message}"); }
    }

    private static void Notify<T>(Action<T> listeners, T value)
    {
        if (listeners == null) return;
        foreach (Action<T> listener in listeners.GetInvocationList())
            try { listener(value); }
            catch (Exception ex) { LogError($"Inventory observer failed: {ex.Message}"); }
    }

    private static void Notify<T, U>(Action<T, U> listeners, T first, U second)
    {
        if (listeners == null) return;
        foreach (Action<T, U> listener in listeners.GetInvocationList())
            try { listener(first, second); }
            catch (Exception ex) { LogError($"Inventory observer failed: {ex.Message}"); }
    }

    private sealed class InventorySnapshot
    {
        readonly HeroData hero;
        readonly List<InventoryItem> inventory;
        readonly Equipment equipment, slots;
        readonly Dictionary<InventoryItem, (int quantity, int slot, int price, Dictionary<string, float> stats)> items = new();
        readonly int bronze, silver, gold;
        readonly UnityEngine.Random.State randomState;

        public InventorySnapshot(HeroData hero)
        {
            this.hero = hero;
            inventory = new List<InventoryItem>(hero.inventory);
            equipment = hero.equipment;
            slots = new Equipment { weapon = equipment.weapon, helmet = equipment.helmet,
                torso = equipment.torso, gloves = equipment.gloves, pants = equipment.pants, boots = equipment.boots };
            foreach (var item in inventory.Concat(new[] { slots.weapon, slots.helmet, slots.torso,
                         slots.gloves, slots.pants, slots.boots }))
                if (item != null && !items.ContainsKey(item))
                    items[item] = (item.quantity, item.slotIndex, item.price, new Dictionary<string, float>(item.GeneratedStats));
            bronze = hero.bronze; silver = hero.silver; gold = hero.gold;
            randomState = UnityEngine.Random.state;
        }

        public void Restore()
        {
            hero.inventory.Clear();
            hero.inventory.AddRange(inventory);
            hero.equipment = equipment;
            equipment.weapon = slots.weapon; equipment.helmet = slots.helmet; equipment.torso = slots.torso;
            equipment.gloves = slots.gloves; equipment.pants = slots.pants; equipment.boots = slots.boots;
            foreach (var pair in items)
            {
                pair.Key.quantity = pair.Value.quantity;
                pair.Key.slotIndex = pair.Value.slot;
                pair.Key.price = pair.Value.price;
                pair.Key.GeneratedStats = pair.Value.stats;
            }
            hero.bronze = bronze; hero.silver = silver; hero.gold = gold;
            UnityEngine.Random.state = randomState;
        }
    }

    // Eventos del inventario
    public static event Action OnInventoryChanged;
    public static event Action OnEquipmentChanged;
    public static event Action<InventoryItem> OnItemAdded;
    public static event Action<InventoryItem> OnItemRemoved;
    public static event Action<InventoryItem, InventoryItem> OnItemEquipped;
    public static event Action<InventoryItem> OnItemUnequipped;
    public static event Action<InventoryItem> OnItemUsed;
    
    // Eventos para confirmación de equipamiento
    public static event Action<InventoryItem, string, UnityAction, UnityAction> OnEquipmentConfirmationRequired;
    public static bool RequestConfirmation(InventoryItem item, string message, Action confirm, Action cancel = null)
    {
        if (OnEquipmentConfirmationRequired == null) return false;
        OnEquipmentConfirmationRequired.Invoke(item, message, () => confirm?.Invoke(), () => cancel?.Invoke());
        return true;
    }

    /// <summary>
    /// Inicializa el servicio con el héroe activo.
    /// </summary>
    public static void Initialize(HeroData hero, Action saveAction = null)
    {
        _currentHero = hero ?? throw new ArgumentNullException(nameof(hero));
        if (saveAction != null) _saveAction = saveAction;
        LogInfo($"Event service initialized for hero: {_currentHero.heroName}");
    }

    #region Event Triggers

    /// <summary>
    /// Dispara el evento de cambio general del inventario y guarda automáticamente.
    /// </summary>
    public static void TriggerInventoryChanged()
    {
        if (_pendingEvents != null) _pendingChange = true;
        else { AutoSaveHeroData(); Notify(OnInventoryChanged); }
    }

    /// <summary>
    /// Dispara el evento de item agregado.
    /// </summary>
    public static void TriggerItemAdded(InventoryItem item)
    {
        if (item == null) return;
        
        Publish(() => Notify(OnItemAdded, item));
        LogInventoryOperation($"Item added: {item.itemId} (Quantity: {item.quantity})");
    }

    /// <summary>
    /// Dispara el evento de item removido.
    /// </summary>
    public static void TriggerItemRemoved(InventoryItem item)
    {
        if (item == null) return;
        
        Publish(() => Notify(OnItemRemoved, item));
        LogInventoryOperation($"Item removed: {item.itemId} (Quantity: {item.quantity})");
    }

    /// <summary>
    /// Dispara el evento de item equipado.
    /// </summary>
    public static void TriggerItemEquipped(InventoryItem equippedItem, InventoryItem unequippedItem = null)
    {
        if (equippedItem == null) return;
        Publish(() => Notify(OnItemEquipped, equippedItem, unequippedItem), equipmentChanged: true);
        
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
        
        Publish(() => Notify(OnItemUnequipped, item), equipmentChanged: true);
        LogInventoryOperation($"Item unequipped: {item.itemId}");
    }

    /// <summary>
    /// Dispara el evento de item usado/consumido.
    /// </summary>
    public static void TriggerItemUsed(InventoryItem item)
    {
        if (item == null) return;
        
        Publish(() => Notify(OnItemUsed, item));
        LogInventoryOperation($"Item used: {item.itemId}");
    }

    /// <summary>
    /// Solicita confirmación para equipar un item que puede causar conflictos.
    /// </summary>
    public static void TriggerEquipmentConfirmationRequired(InventoryItem item, string warningMessage, UnityAction onConfirm, UnityAction onCancel)
    {
        if (item == null) return;
        
        OnEquipmentConfirmationRequired?.Invoke(item, warningMessage, onConfirm, onCancel);
        LogInfo($"Equipment confirmation requested for item: {item.itemId}");
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

        if (_saveAction == null)
        {
            LogWarning("Cannot save - no save action configured (pass saveAction to Initialize)");
            return;
        }

        try
        {
            _saveAction.Invoke();
            LogInfo("Hero data auto-saved successfully");
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

        if (foundInInventory)
        {
            LogWarning($"Equipped {slotName} ({equippedItem.itemId}) is duplicated in inventory");
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
        OnEquipmentChanged = null;
        OnItemAdded = null;
        OnItemRemoved = null;
        OnItemEquipped = null;
        OnItemUnequipped = null;
        OnItemUsed = null;
        OnEquipmentConfirmationRequired = null;
        
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
