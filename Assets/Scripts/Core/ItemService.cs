using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Data.Items;

/// <summary>
/// Servicio centralizado para acceso a datos de ítems.
/// Maneja caché inteligente y proporciona API unificada para todas las operaciones de ItemDataSO.
///
/// Implementado como MonoBehaviour singleton para que el ciclo de vida sea gestionado
/// por Unity (Awake/OnDestroy) en lugar de inicialización manual desde sistemas ECS.
/// Los wrappers estáticos preservan la API pública — 0 callers modificados.
/// </summary>
public class ItemService : MonoBehaviour
{
    private static ItemService _instance;

    public static ItemService Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindFirstObjectByType<ItemService>();
            if (_instance == null)
            {
                var go = new GameObject("[ItemService]");
                _instance = go.AddComponent<ItemService>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    #region Private Fields

    private EnhancedItemDatabase _itemDatabase;
    private Dictionary<string, ItemDataSO> _itemCache;
    private Dictionary<ItemType, List<ItemDataSO>> _itemsByType;
    private Dictionary<ItemCategory, List<ItemDataSO>> _itemsByCategory;
    private Dictionary<ArmorType, List<ItemDataSO>> _itemsByArmorType;
    private Dictionary<ItemRarity, List<ItemDataSO>> _itemsByRarity;
    private bool _isInitialized = false;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeInternal();
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            ClearCaches();
            _isInitialized = false;
            _instance = null;
        }
    }

    #endregion

    #region Initialization

    private void InitializeInternal()
    {
        if (_isInitialized) return;

        LoadDatabase();
        if (_itemDatabase == null) return;

        BuildCaches();
        _isInitialized = true;

        Debug.Log($"[ItemService] Initialized successfully with {_itemCache.Count} items");
    }

    #endregion

    #region Instance Methods

    private ItemDataSO GetItemByIdInternal(string itemId)
    {
        EnsureInitialized();

        if (string.IsNullOrEmpty(itemId)) return null;

        if (_itemCache.TryGetValue(itemId, out ItemDataSO item)) return item;

        Debug.LogWarning($"[ItemService] Item with ID '{itemId}' not found");
        return null;
    }

    private List<ItemDataSO> GetItemsByTypeInternal(ItemType itemType)
    {
        EnsureInitialized();

        if (_itemsByType.TryGetValue(itemType, out List<ItemDataSO> items))
            return new List<ItemDataSO>(items);

        return new List<ItemDataSO>();
    }

    private List<ItemDataSO> GetEquipableItemsInternal(ItemType itemType, ArmorType armorType = ArmorType.None)
    {
        EnsureInitialized();

        var items = GetItemsByTypeInternal(itemType);

        if (itemType == ItemType.Armor && armorType != ArmorType.None)
            items = items.Where(item => item.armorType == armorType).ToList();

        return items.Where(item => item.IsEquipment).ToList();
    }

    private List<ItemDataSO> GetConsumableItemsInternal()
    {
        EnsureInitialized();
        return _itemCache.Values.Where(item => item.IsConsumable).ToList();
    }

    private List<ItemDataSO> SearchItemsByNameInternal(string partialName)
    {
        EnsureInitialized();

        if (string.IsNullOrEmpty(partialName))
            return new List<ItemDataSO>();

        string searchTerm = partialName.ToLower();
        return _itemCache.Values
            .Where(item => item.name.ToLower().Contains(searchTerm))
            .ToList();
    }

    private List<ItemDataSO> GetFilteredItemsInternal(
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

    private ItemSystemStats GetSystemStatsInternal()
    {
        EnsureInitialized();

        return new ItemSystemStats
        {
            totalItemCount      = _itemCache.Count,
            weaponCount         = GetItemsByTypeInternal(ItemType.Weapon).Count,
            armorCount          = GetItemsByTypeInternal(ItemType.Armor).Count,
            consumableCount     = GetItemsByTypeInternal(ItemType.Consumable).Count,
            equipmentCount      = _itemCache.Values.Count(item => item.IsEquipment),
            consumableItemCount = _itemCache.Values.Count(item => item.IsConsumable)
        };
    }

    private void ForceReinitializeInternal()
    {
        _isInitialized = false;
        ClearCaches();
        InitializeInternal();
    }

    #endregion

    #region Private Helpers

    private void EnsureInitialized()
    {
        if (!_isInitialized)
            InitializeInternal();
    }

    private void LoadDatabase()
    {
        _itemDatabase = EnhancedItemDatabase.Instance;
        if (_itemDatabase == null) Debug.LogError("[ItemService] Failed to load EnhancedItemDatabase!");
    }

    private void BuildCaches()
    {
        ClearCaches();

        var allItems = _itemDatabase.GetAllItems();

        _itemCache          = new Dictionary<string, ItemDataSO>();
        _itemsByType        = new Dictionary<ItemType, List<ItemDataSO>>();
        _itemsByCategory    = new Dictionary<ItemCategory, List<ItemDataSO>>();
        _itemsByArmorType   = new Dictionary<ArmorType, List<ItemDataSO>>();
        _itemsByRarity      = new Dictionary<ItemRarity, List<ItemDataSO>>();

        foreach (var item in allItems)
        {
            if (item == null || string.IsNullOrEmpty(item.id)) continue;

            if (_itemCache.ContainsKey(item.id))
            {
                Debug.LogWarning($"[ItemService] Duplicate item ID found: {item.id}");
                continue;
            }
            _itemCache[item.id] = item;

            if (!_itemsByType.ContainsKey(item.itemType))     _itemsByType[item.itemType]         = new List<ItemDataSO>();
            _itemsByType[item.itemType].Add(item);

            if (!_itemsByCategory.ContainsKey(item.itemCategory)) _itemsByCategory[item.itemCategory] = new List<ItemDataSO>();
            _itemsByCategory[item.itemCategory].Add(item);

            if (!_itemsByArmorType.ContainsKey(item.armorType))   _itemsByArmorType[item.armorType]   = new List<ItemDataSO>();
            _itemsByArmorType[item.armorType].Add(item);

            if (!_itemsByRarity.ContainsKey(item.rarity))         _itemsByRarity[item.rarity]         = new List<ItemDataSO>();
            _itemsByRarity[item.rarity].Add(item);
        }

        Debug.Log($"[ItemService] Cache built successfully: {_itemCache.Count} items indexed");
    }

    private void ClearCaches()
    {
        _itemCache?.Clear();
        _itemsByType?.Clear();
        _itemsByCategory?.Clear();
        _itemsByArmorType?.Clear();
        _itemsByRarity?.Clear();
    }

    #endregion

    #region Static API (thin wrappers — no callers modified)

    /// <summary>No-op: initialization is handled automatically in Awake.</summary>
    public static void Initialize() { _ = Instance; }

    public static bool IsInitialized => _instance != null && _instance._isInitialized;

    public static void ForceReinitialize()          => Instance.ForceReinitializeInternal();

    public static ItemDataSO GetItemById(string itemId)
        => Instance.GetItemByIdInternal(itemId);

    public static List<ItemDataSO> GetItemsByType(ItemType itemType)
        => Instance.GetItemsByTypeInternal(itemType);

    public static List<ItemDataSO> GetEquipableItems(ItemType itemType, ArmorType armorType = ArmorType.None)
        => Instance.GetEquipableItemsInternal(itemType, armorType);

    public static List<ItemDataSO> GetConsumableItems()
        => Instance.GetConsumableItemsInternal();

    public static List<ItemDataSO> SearchItemsByName(string partialName)
        => Instance.SearchItemsByNameInternal(partialName);

    public static List<ItemDataSO> GetFilteredItems(
        ItemType? itemType = null,
        ItemCategory? itemCategory = null,
        ItemRarity? rarity = null,
        ArmorType? armorType = null)
        => Instance.GetFilteredItemsInternal(itemType, itemCategory, rarity, armorType);

    public static ItemSystemStats GetSystemStats()
        => Instance.GetSystemStatsInternal();

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
