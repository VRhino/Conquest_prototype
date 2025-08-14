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
    /// Obtiene el InventoryItem equipado en un slot específico.
    /// Centraliza el acceso a los diferentes slots de equipamiento.
    /// </summary>
    /// <param name="equipment">Equipamiento del héroe</param>
    /// <param name="itemType">Tipo de slot</param>
    /// <returns>InventoryItem equipado o null si no hay nada</returns>
    public static InventoryItem GetEquippedItem(Equipment equipment, ItemType itemType)
    {
        if (equipment == null) return null;

        return itemType switch
        {
            ItemType.Weapon => equipment.weapon,
            ItemType.Helmet => equipment.helmet,
            ItemType.Torso => equipment.torso,
            ItemType.Gloves => equipment.gloves,
            ItemType.Pants => equipment.pants,
            ItemType.Boots => equipment.boots,
            _ => null
        };
    }

    /// <summary>
    /// Obtiene el ID del ítem equipado en un slot específico (método de compatibilidad).
    /// </summary>
    /// <param name="equipment">Equipamiento del héroe</param>
    /// <param name="itemType">Tipo de slot</param>
    /// <returns>ID del ítem equipado o string vacío si no hay nada</returns>
    public static string GetEquippedItemId(Equipment equipment, ItemType itemType)
    {
        var item = GetEquippedItem(equipment, itemType);
        return item?.itemId ?? string.Empty;
    }

    /// <summary>
    /// Asigna un InventoryItem a un slot específico de equipamiento.
    /// </summary>
    /// <param name="equipment">Equipamiento del héroe</param>
    /// <param name="itemType">Tipo de slot</param>
    /// <param name="item">InventoryItem a equipar (puede ser null para desequipar)</param>
    public static void SetEquippedItem(Equipment equipment, ItemType itemType, InventoryItem item)
    {
        if (equipment == null) return;

        switch (itemType)
        {
            case ItemType.Weapon:
                equipment.weapon = item;
                break;
            case ItemType.Helmet:
                equipment.helmet = item;
                break;
            case ItemType.Torso:
                equipment.torso = item;
                break;
            case ItemType.Gloves:
                equipment.gloves = item;
                break;
            case ItemType.Pants:
                equipment.pants = item;
                break;
            case ItemType.Boots:
                equipment.boots = item;
                break;
        }
    }

    /// <summary>
    /// Obtiene el slot donde está equipado un ítem específico por instanceId.
    /// </summary>
    /// <param name="equipment">Equipamiento del héroe</param>
    /// <param name="instanceId">InstanceId del ítem a buscar</param>
    /// <returns>Tipo de slot donde está equipado o ItemType.None si no está equipado</returns>
    public static ItemType GetEquippedSlotByInstanceId(Equipment equipment, string instanceId)
    {
        if (equipment == null || string.IsNullOrEmpty(instanceId)) return ItemType.None;

        if (equipment.weapon?.instanceId == instanceId) return ItemType.Weapon;
        if (equipment.helmet?.instanceId == instanceId) return ItemType.Helmet;
        if (equipment.torso?.instanceId == instanceId) return ItemType.Torso;
        if (equipment.gloves?.instanceId == instanceId) return ItemType.Gloves;
        if (equipment.pants?.instanceId == instanceId) return ItemType.Pants;
        if (equipment.boots?.instanceId == instanceId) return ItemType.Boots;

        return ItemType.None;
    }

    /// <summary>
    /// Obtiene el slot donde está equipado un ítem específico por itemId (método de compatibilidad).
    /// </summary>
    /// <param name="equipment">Equipamiento del héroe</param>
    /// <param name="itemId">ID del ítem a buscar</param>
    /// <returns>Tipo de slot donde está equipado o ItemType.None si no está equipado</returns>
    public static ItemType GetEquippedSlot(Equipment equipment, string itemId)
    {
        if (equipment == null || string.IsNullOrEmpty(itemId)) return ItemType.None;

        if (equipment.weapon?.itemId == itemId) return ItemType.Weapon;
        if (equipment.helmet?.itemId == itemId) return ItemType.Helmet;
        if (equipment.torso?.itemId == itemId) return ItemType.Torso;
        if (equipment.gloves?.itemId == itemId) return ItemType.Gloves;
        if (equipment.pants?.itemId == itemId) return ItemType.Pants;
        if (equipment.boots?.itemId == itemId) return ItemType.Boots;

        return ItemType.None;
    }

    /// <summary>
    /// Obtiene todos los ítems equipados como una lista.
    /// </summary>
    /// <param name="equipment">Equipamiento del héroe</param>
    /// <returns>Lista de InventoryItem equipados (excluye nulls)</returns>
    public static List<InventoryItem> GetAllEquippedItems(Equipment equipment)
    {
        var items = new List<InventoryItem>();
        if (equipment == null) return items;

        if (equipment.weapon != null) items.Add(equipment.weapon);
        if (equipment.helmet != null) items.Add(equipment.helmet);
        if (equipment.torso != null) items.Add(equipment.torso);
        if (equipment.gloves != null) items.Add(equipment.gloves);
        if (equipment.pants != null) items.Add(equipment.pants);
        if (equipment.boots != null) items.Add(equipment.boots);

        return items;
    }

    /// <summary>
    /// Verifica si un slot específico está ocupado.
    /// </summary>
    /// <param name="equipment">Equipamiento del héroe</param>
    /// <param name="itemType">Tipo de slot a verificar</param>
    /// <returns>True si hay un ítem equipado en el slot</returns>
    public static bool IsSlotOccupied(Equipment equipment, ItemType itemType)
    {
        return GetEquippedItem(equipment, itemType) != null;
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

    /// <summary>
    /// Encuentra un ítem en el inventario por su posición de slot.
    /// </summary>
    /// <param name="items">Lista de ítems del inventario</param>
    /// <param name="slotIndex">Índice del slot a buscar</param>
    /// <returns>El ítem en esa posición o null si no existe</returns>
    public static InventoryItem GetItemAtSlotIndex(List<InventoryItem> items, int slotIndex)
    {
        if (items == null || slotIndex < 0) return null;
        
        return items.FirstOrDefault(item => item.slotIndex == slotIndex);
    }

    /// <summary>
    /// Verifica si un slot específico está ocupado en una lista de inventario.
    /// </summary>
    /// <param name="items">Lista de ítems del inventario</param>
    /// <param name="slotIndex">Índice del slot a verificar</param>
    /// <returns>True si el slot está ocupado</returns>
    public static bool IsSlotOccupied(List<InventoryItem> items, int slotIndex)
    {
        if (items == null || slotIndex < 0) return false;
        
        return items.Any(item => item.slotIndex == slotIndex);
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

    #region Instance Management Utilities

    /// <summary>
    /// Agrega una instancia de ítem existente al inventario en el primer slot disponible.
    /// No crea nuevas instancias, solo maneja la referencia existente.
    /// </summary>
    /// <param name="inventory">Lista del inventario</param>
    /// <param name="item">Instancia de ítem existente</param>
    /// <param name="maxSlots">Límite máximo de slots</param>
    /// <returns>True si se agregó exitosamente</returns>
    public static bool AddExistingItemToInventory(List<InventoryItem> inventory, InventoryItem item, int maxSlots)
    {
        if (inventory == null || item == null) return false;
        
        // Verificar límite de inventario
        if (inventory.Count >= maxSlots) return false;

        // Para items stackeables, intentar stackear primero
        if (item.IsStackable)
        {
            var existingStack = inventory.FirstOrDefault(i => 
                i.itemId == item.itemId && i.IsStackable);
            
            if (existingStack != null)
            {
                existingStack.quantity += item.quantity;
                return true;
            }
        }

        // Encontrar slot disponible
        int targetSlot = FindNextAvailableSlotIndex(inventory, maxSlots);
        if (targetSlot == -1) return false;

        // Asignar slot y agregar al inventario
        item.slotIndex = targetSlot;
        inventory.Add(item);
        return true;
    }

    /// <summary>
    /// Agrega una instancia de ítem existente al inventario en un slot específico.
    /// </summary>
    /// <param name="inventory">Lista del inventario</param>
    /// <param name="item">Instancia de ítem existente</param>
    /// <param name="targetSlot">Slot específico donde colocar el ítem</param>
    /// <param name="maxSlots">Límite máximo de slots</param>
    /// <returns>True si se agregó exitosamente</returns>
    public static bool AddExistingItemToSlot(List<InventoryItem> inventory, InventoryItem item, int targetSlot, int maxSlots)
    {
        if (inventory == null || item == null) return false;
        if (targetSlot < 0 || targetSlot >= maxSlots) return false;
        
        // Verificar que el slot esté libre
        if (inventory.Any(i => i.slotIndex == targetSlot)) return false;

        // Para items stackeables, intentar stackear primero con otros del mismo tipo
        if (item.IsStackable)
        {
            var existingStack = inventory.FirstOrDefault(i => 
                i.itemId == item.itemId && i.IsStackable);
            
            if (existingStack != null)
            {
                existingStack.quantity += item.quantity;
                return true;
            }
        }

        // Asignar slot específico y agregar al inventario
        item.slotIndex = targetSlot;
        inventory.Add(item);
        return true;
    }

    /// <summary>
    /// Remueve una instancia específica del inventario por referencia.
    /// </summary>
    /// <param name="inventory">Lista del inventario</param>
    /// <param name="item">Instancia específica a remover</param>
    /// <returns>True si se removió exitosamente</returns>
    public static bool RemoveSpecificItemInstance(List<InventoryItem> inventory, InventoryItem item)
    {
        if (inventory == null || item == null) return false;
        
        return inventory.Remove(item);
    }

    /// <summary>
    /// Mueve una instancia de ítem a un nuevo slot en el inventario.
    /// </summary>
    /// <param name="item">Instancia de ítem a mover</param>
    /// <param name="newSlot">Nuevo slot de destino</param>
    /// <param name="inventory">Lista del inventario para validar disponibilidad</param>
    /// <returns>True si se movió exitosamente</returns>
    public static bool MoveItemToSlot(InventoryItem item, int newSlot, List<InventoryItem> inventory)
    {
        if (item == null || inventory == null) return false;
        if (newSlot < 0) return false;
        
        // Verificar que el nuevo slot esté libre (excepto el slot actual del ítem)
        if (inventory.Any(i => i.slotIndex == newSlot && i != item)) return false;

        item.slotIndex = newSlot;
        return true;
    }

    /// <summary>
    /// Intercambia las posiciones de dos ítems en el inventario.
    /// </summary>
    /// <param name="item1">Primer ítem</param>
    /// <param name="item2">Segundo ítem</param>
    /// <returns>True si se intercambiaron exitosamente</returns>
    public static bool SwapItemSlots(InventoryItem item1, InventoryItem item2)
    {
        if (item1 == null || item2 == null) return false;
        
        int tempSlot = item1.slotIndex;
        item1.slotIndex = item2.slotIndex;
        item2.slotIndex = tempSlot;
        return true;
    }

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

    #region Equipment Swap Utilities

    /// <summary>
    /// Intercambia un item del inventario con un item equipado, preservando el slot original.
    /// </summary>
    /// <param name="inventoryItem">Item del inventario que se va a equipar</param>
    /// <param name="equippedItem">Item equipado que se va a devolver al inventario</param>
    /// <param name="targetSlot">Slot específico donde colocar el item equipado</param>
    /// <returns>True si el intercambio fue exitoso</returns>
    public static bool SwapInventoryAndEquipment(InventoryItem inventoryItem, InventoryItem equippedItem, int targetSlot)
    {
        if (inventoryItem == null)
        {
            Debug.LogError("[InventoryUtils] SwapInventoryAndEquipment: inventoryItem is null");
            return false;
        }

        if (equippedItem == null)
        {
            Debug.LogWarning("[InventoryUtils] SwapInventoryAndEquipment: equippedItem is null, only removing from inventory");
            return true; // No hay nada que intercambiar
        }

        // Verificar que el slot target está libre o es el slot original del inventoryItem
        var heroData = PlayerSessionService.SelectedHero;
        if (heroData?.inventory == null) return false;

        var itemInTargetSlot = heroData.inventory.FirstOrDefault(i => i.slotIndex == targetSlot);
        if (itemInTargetSlot != null && itemInTargetSlot != inventoryItem)
        {
            Debug.LogWarning($"[InventoryUtils] SwapInventoryAndEquipment: Target slot {targetSlot} is occupied by different item {itemInTargetSlot.itemId}");
            return false;
        }

        // Configurar el slot del item equipado
        equippedItem.slotIndex = targetSlot;
        
        Debug.Log($"[InventoryUtils] SwapInventoryAndEquipment: {inventoryItem.itemId} (slot {inventoryItem.slotIndex}) <-> {equippedItem.itemId} (to slot {targetSlot})");
        return true;
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
