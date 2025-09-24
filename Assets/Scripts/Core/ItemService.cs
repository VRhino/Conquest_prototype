using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Data.Items;

/// <summary>
/// Servicio centralizado para acceso a datos de ítems.
/// Maneja caché inteligente y proporciona API unificada para todas las operaciones de ItemDataSO.
/// </summary>
public static class ItemService
{
    #region Private Fields
    
    private static EnhancedItemDatabase _itemDatabase;
    private static Dictionary<string, ItemDataSO> _itemCache;
    private static Dictionary<ItemType, List<ItemDataSO>> _itemsByType;
    private static Dictionary<ItemCategory, List<ItemDataSO>> _itemsByCategory;
    private static Dictionary<ArmorType, List<ItemDataSO>> _itemsByArmorType;
    private static Dictionary<ItemRarity, List<ItemDataSO>> _itemsByRarity;
    private static bool _isInitialized = false;
    
    #endregion
    
    #region Initialization
    
    /// <summary>
    /// Inicializa el servicio cargando la base de datos y preparando los caches.
    /// </summary>
    public static void Initialize()
    {
        if (_isInitialized) return;
        
        LoadDatabase();
        if (_itemDatabase == null) return;
        
        BuildCaches();
        _isInitialized = true;
        
        Debug.Log($"[ItemService] Initialized successfully with {_itemCache.Count} items");
    }
    
    /// <summary>
    /// Verifica si el servicio está inicializado.
    /// </summary>
    public static bool IsInitialized => _isInitialized;
    
    /// <summary>
    /// Fuerza la reinicialización del servicio (útil para testing o cambios en runtime).
    /// </summary>
    public static void ForceReinitialize()
    {
        _isInitialized = false;
        ClearCaches();
        Initialize();
    }
    
    #endregion
    
    #region Core API
    
    /// <summary>
    /// Obtiene un ItemDataSO por su ID.
    /// </summary>
    /// <param name="itemId">ID único del ítem</param>
    /// <returns>ItemDataSO encontrado o null si no existe</returns>
    public static ItemDataSO GetItemById(string itemId)
    {
        EnsureInitialized();
        
        if (string.IsNullOrEmpty(itemId)) return null;
        
        if (_itemCache.TryGetValue(itemId, out ItemDataSO item)) return item;
        
        Debug.LogWarning($"[ItemService] Item with ID '{itemId}' not found");
        return null;
    }
    
    /// <summary>
    /// Obtiene todos los ítems de un tipo específico.
    /// </summary>
    /// <param name="itemType">Tipo de ítem a filtrar</param>
    /// <returns>Lista de ítems del tipo especificado</returns>
    public static List<ItemDataSO> GetItemsByType(ItemType itemType)
    {
        EnsureInitialized();
        
        if (_itemsByType.TryGetValue(itemType, out List<ItemDataSO> items))
            return new List<ItemDataSO>(items);
        
        return new List<ItemDataSO>();
    }
    
    #endregion

    #region Advanced API

    /// <summary>
    /// Obtiene ítems equipables para un slot específico (tipo + tipo de armadura).
    /// </summary>
    /// <param name="itemType">Tipo de ítem (Weapon, Armor, etc.)</param>
    /// <param name="armorType">Tipo de armadura (solo aplicable para armaduras)</param>
    /// <returns>Lista de ítems equipables en el slot especificado</returns>
    public static List<ItemDataSO> GetEquipableItems(ItemType itemType, ArmorType armorType = ArmorType.None)
    {
        EnsureInitialized();
        
        var items = GetItemsByType(itemType);
        
        if (itemType == ItemType.Armor && armorType != ArmorType.None)
        {
            items = items.Where(item => item.armorType == armorType).ToList();
        }
        
        return items.Where(item => item.IsEquipment).ToList();
    }
    
    /// <summary>
    /// Obtiene ítems consumibles.
    /// </summary>
    /// <returns>Lista de todos los ítems consumibles</returns>
    public static List<ItemDataSO> GetConsumableItems()
    {
        EnsureInitialized();
        return _itemCache.Values.Where(item => item.IsConsumable).ToList();
    }
    
    /// <summary>
    /// Busca ítems por nombre (búsqueda parcial, case-insensitive).
    /// </summary>
    /// <param name="partialName">Nombre parcial a buscar</param>
    /// <returns>Lista de ítems que coinciden</returns>
    public static List<ItemDataSO> SearchItemsByName(string partialName)
    {
        EnsureInitialized();
        
        if (string.IsNullOrEmpty(partialName))
            return new List<ItemDataSO>();
        
        string searchTerm = partialName.ToLower();
        return _itemCache.Values
            .Where(item => item.name.ToLower().Contains(searchTerm))
            .ToList();
    }
    
    /// <summary>
    /// Obtiene ítems filtrados por múltiples criterios.
    /// </summary>
    /// <param name="itemType">Filtro por tipo (opcional)</param>
    /// <param name="itemCategory">Filtro por categoría (opcional)</param>
    /// <param name="rarity">Filtro por rareza (opcional)</param>
    /// <param name="armorType">Filtro por tipo de armadura (opcional)</param>
    /// <returns>Lista de ítems que cumplen todos los criterios especificados</returns>
    public static List<ItemDataSO> GetFilteredItems(
        ItemType? itemType = null, 
        ItemCategory? itemCategory = null, 
        ItemRarity? rarity = null, 
        ArmorType? armorType = null)
    {
        EnsureInitialized();
        
        var items = _itemCache.Values.AsEnumerable();
        
        if (itemType.HasValue && itemType.Value != ItemType.None)
            items = items.Where(item => item.itemType == itemType.Value);
        
        if (itemCategory.HasValue && itemCategory.Value != ItemCategory.None)
            items = items.Where(item => item.itemCategory == itemCategory.Value);
        
        if (rarity.HasValue)
            items = items.Where(item => item.rarity == rarity.Value);
        
        if (armorType.HasValue && armorType.Value != ArmorType.None)
            items = items.Where(item => item.armorType == armorType.Value);
        
        return items.ToList();
    }
    
    #endregion
    
    #region Statistics API
    
    /// <summary>
    /// Obtiene estadísticas del sistema de ítems.
    /// </summary>
    /// <returns>Información sobre el estado del sistema</returns>
    public static ItemSystemStats GetSystemStats()
    {
        EnsureInitialized();
        
        var stats = new ItemSystemStats
        {
            totalItemCount = _itemCache.Count,
            weaponCount = GetItemsByType(ItemType.Weapon).Count,
            armorCount = GetItemsByType(ItemType.Armor).Count,
            consumableCount = GetItemsByType(ItemType.Consumable).Count,
            equipmentCount = _itemCache.Values.Count(item => item.IsEquipment),
            consumableItemCount = _itemCache.Values.Count(item => item.IsConsumable)
        };
        
        return stats;
    }
    
    #endregion
    
    #region Private Methods
    
    /// <summary>
    /// Asegura que el servicio esté inicializado antes de cualquier operación.
    /// </summary>
    private static void EnsureInitialized()
    {
        if (!_isInitialized)
        {
            Initialize();
        }
    }
    
    /// <summary>
    /// Carga la base de datos de ítems.
    /// </summary>
    private static void LoadDatabase()
    {
        _itemDatabase = EnhancedItemDatabase.Instance;
        if (_itemDatabase == null) Debug.LogError("[ItemService] Failed to load EnhancedItemDatabase!");
    }
    
    /// <summary>
    /// Construye todos los caches para optimizar búsquedas.
    /// </summary>
    private static void BuildCaches()
    {
        ClearCaches();
        
        var allItems = _itemDatabase.GetAllItems();
        
        _itemCache = new Dictionary<string, ItemDataSO>();
        _itemsByType = new Dictionary<ItemType, List<ItemDataSO>>();
        _itemsByCategory = new Dictionary<ItemCategory, List<ItemDataSO>>();
        _itemsByArmorType = new Dictionary<ArmorType, List<ItemDataSO>>();
        _itemsByRarity = new Dictionary<ItemRarity, List<ItemDataSO>>();
        
        foreach (var item in allItems)
        {
            if (item == null || string.IsNullOrEmpty(item.id)) continue;
            
            // Cache principal por ID
            if (_itemCache.ContainsKey(item.id))
            {
                Debug.LogWarning($"[ItemService] Duplicate item ID found: {item.id}");
                continue;
            }
            _itemCache[item.id] = item;
            
            // Cache por tipo
            if (!_itemsByType.ContainsKey(item.itemType)) _itemsByType[item.itemType] = new List<ItemDataSO>();
            _itemsByType[item.itemType].Add(item);
            
            // Cache por categoría
            if (!_itemsByCategory.ContainsKey(item.itemCategory)) _itemsByCategory[item.itemCategory] = new List<ItemDataSO>();
            _itemsByCategory[item.itemCategory].Add(item);
            
            // Cache por tipo de armadura
            if (!_itemsByArmorType.ContainsKey(item.armorType)) _itemsByArmorType[item.armorType] = new List<ItemDataSO>();
            _itemsByArmorType[item.armorType].Add(item);
            
            // Cache por rareza
            if (!_itemsByRarity.ContainsKey(item.rarity)) _itemsByRarity[item.rarity] = new List<ItemDataSO>();
            _itemsByRarity[item.rarity].Add(item);
        }
        
        Debug.Log($"[ItemService] Cache built successfully: {_itemCache.Count} items indexed");
    }
    
    /// <summary>
    /// Limpia todos los caches.
    /// </summary>
    private static void ClearCaches()
    {
        _itemCache?.Clear();
        _itemsByType?.Clear();
        _itemsByCategory?.Clear();
        _itemsByArmorType?.Clear();
        _itemsByRarity?.Clear();
    }
    
    #endregion
    
    #region Data Structures
    
    [System.Serializable]
    public struct ItemSystemStats
    {
        public int totalItemCount;
        public int weaponCount;
        public int armorCount;
        public int consumableCount;
        public int equipmentCount;
        public int consumableItemCount;
    }
    
    #endregion
}