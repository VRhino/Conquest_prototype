using System;
using System.Collections.Generic;
using UnityEngine;
using Data.Items;

/// <summary>
/// Facade principal del sistema de inventario que coordina todos los servicios especializados.
/// Proporciona una API unificada y simplificada para el manejo completo del inventario.
/// 
/// ARQUITECTURA FACADE:
/// - InventoryStorageService: Operaciones básicas de almacenamiento
/// - InventoryEventService: Eventos y persistencia
/// - EquipmentManagerService: Lógica de equipamiento
/// - ConsumableManagerService: Lógica de consumibles
/// - ItemInstanceService: Creación de instancias (existente)
/// </summary>
public static class InventoryManager
{
    private static HeroData _currentHero;
    private static bool _isInitialized = false;

    /// <summary>Límite máximo de slots del inventario.</summary>
    public static int InventoryLimit 
    { 
        get => InventoryStorageService.InventoryLimit; 
        set => InventoryStorageService.InventoryLimit = value; 
    }

    #region Initialization

    /// <summary>
    /// Inicializa el sistema completo de inventario con el héroe activo.
    /// </summary>
    public static void Initialize(HeroData hero)
    {
        if (hero == null)
        {
            throw new ArgumentNullException(nameof(hero), "Hero cannot be null");
        }

        _currentHero = hero;

        // Inicializar todos los servicios especializados
        InventoryStorageService.Initialize(hero);
        InventoryEventService.Initialize(hero);
        EquipmentManagerService.Initialize(hero);
        ConsumableManagerService.Initialize(hero);

        _isInitialized = true;
        LogInfo($"Inventory system initialized for hero: {hero.heroName}");
        
        // Validar integridad después de la inicialización
        InventoryEventService.ValidateInventoryIntegrity();
    }

    /// <summary>
    /// Verifica si el sistema está inicializado.
    /// </summary>
    public static bool IsInitialized => _isInitialized && _currentHero != null;

    /// <summary>
    /// Obtiene el héroe actual.
    /// </summary>
    public static HeroData GetCurrentHero() => _currentHero;

    #endregion

    #region Item Addition (Create + Add)

    /// <summary>
    /// Crea una nueva instancia de item y la agrega al inventario.
    /// </summary>
    public static bool AddItem(string itemId, int quantity = 1)
    {
        if (!ValidateInitialization()) return false;

        var newItem = ItemInstanceService.CreateItem(itemId, quantity);
        if (newItem == null)
        {
            LogError($"Failed to create item: {itemId}");
            return false;
        }

        bool success = InventoryStorageService.AddExistingItem(newItem);
        if (success)
        {
            InventoryEventService.TriggerItemAdded(newItem);
        }

        return success;
    }

    /// <summary>
    /// Crea una nueva instancia de item y la agrega en un slot específico.
    /// </summary>
    public static bool AddItemAtSlot(string itemId, int quantity, int targetSlotIndex)
    {
        if (!ValidateInitialization()) return false;

        var newItem = ItemInstanceService.CreateItem(itemId, quantity);
        if (newItem == null)
        {
            LogError($"Failed to create item: {itemId}");
            return false;
        }

        bool success = InventoryStorageService.AddExistingItemAtSlot(newItem, targetSlotIndex);
        if (success)
        {
            InventoryEventService.TriggerItemAdded(newItem);
        }

        return success;
    }

    #endregion

    #region Existing Item Operations

    /// <summary>
    /// Agrega un item ya existente al inventario (preserva instanceId y stats).
    /// </summary>
    public static bool AddExistingItem(InventoryItem item)
    {
        if (!ValidateInitialization()) return false;

        bool success = InventoryStorageService.AddExistingItem(item);
        if (success)
        {
            InventoryEventService.TriggerItemAdded(item);
        }

        return success;
    }

    /// <summary>
    /// Agrega un item existente en un slot específico.
    /// </summary>
    public static bool AddExistingItemAtSlot(InventoryItem item, int targetSlotIndex)
    {
        if (!ValidateInitialization()) return false;

        bool success = InventoryStorageService.AddExistingItemAtSlot(item, targetSlotIndex);
        if (success)
        {
            InventoryEventService.TriggerItemAdded(item);
        }

        return success;
    }

    /// <summary>
    /// Remueve un item específico del inventario por referencia.
    /// </summary>
    public static bool RemoveSpecificItem(InventoryItem item)
    {
        if (!ValidateInitialization()) return false;

        bool success = InventoryStorageService.RemoveSpecificItem(item);
        if (success)
        {
            InventoryEventService.TriggerItemRemoved(item);
        }

        return success;
    }

    /// <summary>
    /// Mueve un item a un slot específico.
    /// </summary>
    public static bool MoveItemToSlot(InventoryItem item, int targetSlotIndex)
    {
        if (!ValidateInitialization()) return false;

        return InventoryStorageService.MoveItemToSlot(item, targetSlotIndex);
    }

    /// <summary>
    /// Intercambia las posiciones de dos items.
    /// </summary>
    public static bool SwapItemSlots(InventoryItem item1, InventoryItem item2)
    {
        if (!ValidateInitialization()) return false;

        return InventoryStorageService.SwapItemSlots(item1, item2);
    }

    #endregion

    #region Item Removal

    /// <summary>
    /// Remueve una cantidad específica de un item stackable.
    /// </summary>
    public static bool RemoveItem(string itemId, int quantity = 1)
    {
        if (!ValidateInitialization()) return false;

        return InventoryStorageService.RemoveItemQuantity(itemId, quantity);
    }

    #endregion

    #region Equipment Operations

    /// <summary>
    /// Equipa un item del inventario. Maneja intercambio automático si hay algo equipado.
    /// </summary>
    public static bool EquipItem(InventoryItem item)
    {
        if (!ValidateInitialization()) return false;

        return EquipmentManagerService.EquipItem(item);
    }

    /// <summary>
    /// Desequipa un item de un tipo específico y lo devuelve al inventario.
    /// </summary>
    public static bool UnequipItem(ItemType itemType)
    {
        if (!ValidateInitialization()) return false;

        return EquipmentManagerService.UnequipItem(itemType);
    }

    /// <summary>
    /// Obtiene el item equipado en un slot específico.
    /// </summary>
    public static InventoryItem GetEquippedItem(ItemType itemType)
    {
        if (!ValidateInitialization()) return null;

        return EquipmentManagerService.GetEquippedItem(itemType);
    }

    /// <summary>
    /// Verifica si un item puede ser equipado.
    /// </summary>
    public static bool CanEquipItem(InventoryItem item)
    {
        if (!ValidateInitialization()) return false;

        return EquipmentManagerService.CanEquipItem(item);
    }

    /// <summary>
    /// Verifica si un item específico está equipado.
    /// </summary>
    public static bool IsItemEquipped(InventoryItem item)
    {
        if (!ValidateInitialization()) return false;

        return EquipmentManagerService.IsItemEquipped(item);
    }

    /// <summary>
    /// Obtiene todos los items equipados.
    /// </summary>
    public static InventoryItem[] GetAllEquippedItems()
    {
        if (!ValidateInitialization()) return new InventoryItem[0];

        return EquipmentManagerService.GetAllEquippedItems();
    }

    #endregion

    #region Consumable Operations

    /// <summary>
    /// Usa un consumible, aplicando sus efectos.
    /// </summary>
    public static bool UseConsumable(InventoryItem item)
    {
        if (!ValidateInitialization()) return false;

        return ConsumableManagerService.UseConsumable(item);
    }

    /// <summary>
    /// Usa múltiples unidades de un consumible.
    /// </summary>
    public static bool UseConsumableQuantity(InventoryItem item, int quantity)
    {
        if (!ValidateInitialization()) return false;

        return ConsumableManagerService.UseConsumableQuantity(item, quantity);
    }

    /// <summary>
    /// Verifica si un item puede ser usado como consumible.
    /// </summary>
    public static bool CanUseItem(InventoryItem item)
    {
        if (!ValidateInitialization()) return false;

        return ConsumableManagerService.CanUseItem(item);
    }

    /// <summary>
    /// Combina dos stacks del mismo item.
    /// </summary>
    public static bool CombineStacks(InventoryItem targetStack, InventoryItem sourceStack)
    {
        if (!ValidateInitialization()) return false;

        return ConsumableManagerService.CombineStacks(targetStack, sourceStack);
    }

    /// <summary>
    /// Divide un stack en dos partes.
    /// </summary>
    public static bool SplitStack(InventoryItem originalStack, int quantityToSplit)
    {
        if (!ValidateInitialization()) return false;

        return ConsumableManagerService.SplitStack(originalStack, quantityToSplit);
    }

    #endregion

    #region Query Operations

    /// <summary>
    /// Obtiene todos los items del inventario.
    /// </summary>
    public static List<InventoryItem> GetAllItems()
    {
        if (!ValidateInitialization()) return new List<InventoryItem>();

        return InventoryStorageService.GetAllItems();
    }

    /// <summary>
    /// Obtiene todos los items de un tipo específico.
    /// </summary>
    public static List<InventoryItem> GetItemsOfType(ItemType itemType)
    {
        if (!ValidateInitialization()) return new List<InventoryItem>();

        return InventoryStorageService.GetItemsOfType(itemType);
    }

    /// <summary>
    /// Busca un item por su instanceId único.
    /// </summary>
    public static InventoryItem FindItemByInstanceId(string instanceId)
    {
        if (!ValidateInitialization()) return null;

        return InventoryStorageService.FindItemByInstanceId(instanceId);
    }

    /// <summary>
    /// Cuenta la cantidad total de un item específico.
    /// </summary>
    public static int GetItemCount(string itemId)
    {
        if (!ValidateInitialization()) return 0;

        return InventoryStorageService.GetItemCount(itemId);
    }

    /// <summary>
    /// Verifica si hay espacio disponible en el inventario.
    /// </summary>
    public static bool HasSpace()
    {
        if (!ValidateInitialization()) return false;

        return InventoryStorageService.HasSpace();
    }

    /// <summary>
    /// Obtiene el item en un slot específico.
    /// </summary>
    public static InventoryItem GetItemAtSlot(int slotIndex)
    {
        if (!ValidateInitialization()) return null;

        return InventoryStorageService.GetItemAtSlot(slotIndex);
    }

    /// <summary>
    /// Obtiene todos los consumibles disponibles.
    /// </summary>
    public static List<InventoryItem> GetAvailableConsumables()
    {
        if (!ValidateInitialization()) return new List<InventoryItem>();

        return ConsumableManagerService.GetAvailableConsumables();
    }

    #endregion

    #region Statistics and Analysis

    /// <summary>
    /// Calcula el total de stats proporcionados por todo el equipment equipado.
    /// </summary>
    public static Dictionary<string, float> CalculateTotalEquipmentStats()
    {
        if (!ValidateInitialization()) return new Dictionary<string, float>();

        return EquipmentManagerService.CalculateTotalEquipmentStats();
    }

    /// <summary>
    /// Cuenta cuántos slots de equipment están ocupados.
    /// </summary>
    public static int GetEquippedItemCount()
    {
        if (!ValidateInitialization()) return 0;

        return EquipmentManagerService.GetEquippedItemCount();
    }

    #endregion

    #region Event Management

    /// <summary>
    /// Eventos del inventario para suscripción externa.
    /// </summary>
    public static event Action OnInventoryChanged
    {
        add => InventoryEventService.OnInventoryChanged += value;
        remove => InventoryEventService.OnInventoryChanged -= value;
    }

    public static event Action<InventoryItem> OnItemAdded
    {
        add => InventoryEventService.OnItemAdded += value;
        remove => InventoryEventService.OnItemAdded -= value;
    }

    public static event Action<InventoryItem> OnItemRemoved
    {
        add => InventoryEventService.OnItemRemoved += value;
        remove => InventoryEventService.OnItemRemoved -= value;
    }

    public static event Action<InventoryItem, InventoryItem> OnItemEquipped
    {
        add => InventoryEventService.OnItemEquipped += value;
        remove => InventoryEventService.OnItemEquipped -= value;
    }

    public static event Action<InventoryItem> OnItemUnequipped
    {
        add => InventoryEventService.OnItemUnequipped += value;
        remove => InventoryEventService.OnItemUnequipped -= value;
    }

    public static event Action<InventoryItem> OnItemUsed
    {
        add => InventoryEventService.OnItemUsed += value;
        remove => InventoryEventService.OnItemUsed -= value;
    }

    /// <summary>
    /// Fuerza el guardado manual de los datos.
    /// </summary>
    public static void ForceSave()
    {
        if (!ValidateInitialization()) return;

        InventoryEventService.ForceSaveHeroData();
    }

    #endregion

    #region Validation and Debugging

    /// <summary>
    /// Valida la integridad completa del sistema de inventario.
    /// </summary>
    public static bool ValidateInventoryIntegrity()
    {
        if (!ValidateInitialization()) return false;

        return InventoryEventService.ValidateInventoryIntegrity();
    }

    /// <summary>
    /// Genera un reporte completo del inventario y equipment.
    /// </summary>
    public static string GenerateFullReport()
    {
        if (!ValidateInitialization()) return "System not initialized";

        var report = new System.Text.StringBuilder();
        report.AppendLine("=== COMPLETE INVENTORY SYSTEM REPORT ===");
        report.AppendLine($"Hero: {_currentHero.heroName}");
        report.AppendLine($"System Status: {(IsInitialized ? "Initialized" : "Not Initialized")}");
        report.AppendLine();

        // Reporte del inventario
        report.AppendLine(InventoryEventService.GenerateInventoryReport());
        report.AppendLine();

        // Reporte del equipment
        report.AppendLine(EquipmentManagerService.GenerateEquipmentReport());
        report.AppendLine();

        // Reporte de consumibles
        report.AppendLine(ConsumableManagerService.GenerateConsumablesReport());

        return report.ToString();
    }

    #endregion

    #region Cleanup

    /// <summary>
    /// Limpia el sistema y libera recursos. Útil al cambiar de héroe.
    /// </summary>
    public static void Cleanup()
    {
        if (_isInitialized)
        {
            // Guardar antes de limpiar
            InventoryEventService.ForceSaveHeroData();
            
            // Limpiar event listeners
            InventoryEventService.ClearAllEventListeners();
        }

        _currentHero = null;
        _isInitialized = false;
        
        LogInfo("Inventory system cleaned up");
    }

    #endregion

    #region Private Helper Methods

    private static bool ValidateInitialization()
    {
        if (!IsInitialized)
        {
            LogError("Inventory system not initialized. Call Initialize() first.");
            return false;
        }
        return true;
    }

    #endregion

    #region Logging

    private static void LogInfo(string message)
    {
        Debug.Log($"[InventoryManager] {message}");
    }

    private static void LogError(string message)
    {
        Debug.LogError($"[InventoryManager] {message}");
    }

    #endregion
}
