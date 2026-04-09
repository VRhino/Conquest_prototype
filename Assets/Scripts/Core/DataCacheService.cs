using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Provides cached access to dynamic hero data such as calculated attributes,
/// unlocked formations and active perks. This avoids recalculating values on
/// every scene load or gameplay tick.
///
/// Implemented as a MonoBehaviour singleton so that coroutines and lifecycle
/// events (Awake/OnDestroy) are managed by Unity instead of static hacks.
/// Callers use the thin static wrappers below — call sites don't need to change.
/// </summary>
public class DataCacheService : MonoBehaviour
{
    private static DataCacheService _instance;

    public static DataCacheService Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindFirstObjectByType<DataCacheService>();
            if (_instance == null)
            {
                var go = new GameObject("[DataCacheService]");
                _instance = go.AddComponent<DataCacheService>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    // Cache of calculated attributes by hero identifier.
    private readonly Dictionary<string, HeroCalculatedAttributes> _attributeCache = new();

    // Cache of unlocked formation IDs per squad instance.
    private readonly Dictionary<string, List<string>> _formationCache = new();

    // Cache of active perk identifiers by hero.
    private readonly Dictionary<string, List<string>> _perkCache = new();

    // Equipment event listeners management
    private bool _eventListenersInitialized = false;
    private Coroutine _debounceCoroutine = null;
    private bool _updatePending = false;

    // Base leadership value for all heroes
    private const float BASE_LEADERSHIP_VALUE = 700f;

    #region Events

    /// <summary>
    /// Evento disparado cuando el cache de un héroe es actualizado.
    /// Útil para que la UI se refresque automáticamente.
    /// </summary>
    public static System.Action<string> OnHeroCacheUpdated;

    #endregion

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    private void OnDestroy()
    {
        CleanupEventListenersInternal();
        if (_instance == this)
            _instance = null;
    }

    // ─── Instance methods ───────────────────────────────────────────────────

    /// <summary>Clears all cached information. Useful when reloading player data.</summary>
    public void ClearInternal()
    {
        _attributeCache.Clear();
        _formationCache.Clear();
        _perkCache.Clear();
    }

    /// <summary>Calculates and stores the derived attributes for the given hero.</summary>
    public void CacheAttributesInternal(HeroData heroData)
    {
        if (heroData == null) return;

        var newAttributes = CalculateAttributesInternal(heroData);
        string key = GetHeroKey(heroData);
        _attributeCache[key] = newAttributes;
        OnHeroCacheUpdated?.Invoke(key);
    }

    public HeroCalculatedAttributes GetHeroCalculatedAttributesInternal(string heroId)
    {
        if (string.IsNullOrEmpty(heroId))
            return HeroCalculatedAttributes.Empty;
        _attributeCache.TryGetValue(heroId, out var attributes);
        return attributes;
    }

    public void CacheFormationsInternal(string squadId, List<string> formations)
    {
        if (string.IsNullOrEmpty(squadId) || formations == null) return;
        _formationCache[squadId] = new List<string>(formations);
    }

    public List<string> GetUnlockedFormationsInternal(string squadId)
    {
        if (string.IsNullOrEmpty(squadId)) return null;
        _formationCache.TryGetValue(squadId, out var list);
        return list;
    }

    public void CachePerksInternal(string heroId, List<string> perks)
    {
        if (string.IsNullOrEmpty(heroId) || perks == null) return;
        _perkCache[heroId] = new List<string>(perks);
    }

    public List<string> GetActivePerksInternal(string heroId)
    {
        if (string.IsNullOrEmpty(heroId)) return null;
        _perkCache.TryGetValue(heroId, out var list);
        return list;
    }

    // ─── Attribute Calculation ───────────────────────────────────────────────

    public static HeroBaseStats GetBaseStats(IHeroProgression progression)
    {
        return HeroBaseStats.FromHeroData(progression);
    }

    public static EquipmentBonuses GetEquipmentBonuses()
    {
        var equipmentStats = EquipmentManagerService.CalculateTotalEquipmentStats();
        return new EquipmentBonuses
        {
            strengthBonus  = equipmentStats.ContainsKey("Strength")  ? (int)equipmentStats["Strength"]  : 0,
            dexterityBonus = equipmentStats.ContainsKey("Dexterity") ? (int)equipmentStats["Dexterity"] : 0,
            armorBonus     = equipmentStats.ContainsKey("Armor")     ? (int)equipmentStats["Armor"]     : 0,
            vitalityBonus  = equipmentStats.ContainsKey("Vitality")  ? (int)equipmentStats["Vitality"]  : 0
        };
    }

    public static HeroCalculatedAttributes CalculateAttributesInternal(HeroData heroData, EquipmentBonuses temporaryMods = default)
    {
        if (heroData == null) return HeroCalculatedAttributes.Empty;
        var baseStats        = GetBaseStats(heroData);
        var equipmentBonuses = GetEquipmentBonuses();
        var classDefinition  = HeroClassManager.GetClassDefinition(heroData.classId);
        return HeroCalculatedAttributes.Calculate(baseStats, equipmentBonuses, temporaryMods, classDefinition);
    }

    public static float GetPureBaseValue(IHeroProgression progression, string attributeName)
    {
        if (progression == null || string.IsNullOrEmpty(attributeName)) return 0f;
        switch (attributeName.ToLower())
        {
            case "strength":   return progression.Strength;
            case "dexterity":  return progression.Dexterity;
            case "armor":      return progression.Armor;
            case "vitality":   return progression.Vitality;
            case "leadership": return BASE_LEADERSHIP_VALUE;
            default:
                Debug.LogWarning($"[DataCacheService] Atributo no reconocido: {attributeName}");
                return 0f;
        }
    }

    public static float GetEquipmentBonusValue(IHeroProgression progression, string attributeName)
    {
        if (progression == null || string.IsNullOrEmpty(attributeName)) return 0f;
        var equipmentBonuses = GetEquipmentBonuses();
        switch (attributeName.ToLower())
        {
            case "strength":  return equipmentBonuses.strengthBonus;
            case "dexterity": return equipmentBonuses.dexterityBonus;
            case "armor":     return equipmentBonuses.armorBonus;
            case "vitality":  return equipmentBonuses.vitalityBonus;
            default:
                Debug.LogWarning($"[DataCacheService] Atributo no reconocido: {attributeName}");
                return 0f;
        }
    }

    public static float GetBaseWithEquipment(IHeroProgression progression, string attributeName)
    {
        return GetPureBaseValue(progression, attributeName) + GetEquipmentBonusValue(progression, attributeName);
    }

    public static float GetFinalCalculatedValue(IHeroProgression progression, string attributeName, EquipmentBonuses temporaryMods = default)
    {
        var baseWithEquipment = GetBaseWithEquipment(progression, attributeName);
        if (!temporaryMods.HasBonuses) return baseWithEquipment;

        float temporaryBonus = 0f;
        switch (attributeName.ToLower())
        {
            case "strength":  temporaryBonus = temporaryMods.strengthBonus;  break;
            case "dexterity": temporaryBonus = temporaryMods.dexterityBonus; break;
            case "armor":     temporaryBonus = temporaryMods.armorBonus;     break;
            case "vitality":  temporaryBonus = temporaryMods.vitalityBonus;  break;
        }
        return baseWithEquipment + temporaryBonus;
    }

    public static float getHeroLeadership(string heroName)
    {
        return Instance.GetHeroCalculatedAttributesInternal(heroName).leadership;
    }

    // ─── Equipment Event Listeners ───────────────────────────────────────────

    public void InitializeEventListenersInternal()
    {
        if (_eventListenersInitialized) return;
        try
        {
            InventoryEventService.OnItemEquipped   += OnItemEquipped;
            InventoryEventService.OnItemUnequipped += OnItemUnequipped;
            _eventListenersInitialized = true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DataCacheService] Failed to initialize event listeners: {e.Message}");
        }
    }

    public void CleanupEventListenersInternal()
    {
        if (!_eventListenersInitialized) return;
        try
        {
            InventoryEventService.OnItemEquipped   -= OnItemEquipped;
            InventoryEventService.OnItemUnequipped -= OnItemUnequipped;
            _eventListenersInitialized = false;

            if (_debounceCoroutine != null)
            {
                StopCoroutine(_debounceCoroutine);
                _debounceCoroutine = null;
                _updatePending = false;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DataCacheService] Failed to cleanup event listeners: {e.Message}");
        }
    }

    private void OnItemEquipped(InventoryItem equippedItem, InventoryItem unequippedItem)
    {
        if (equippedItem?.IsEquipment == true) ScheduleCacheUpdate();
    }

    private void OnItemUnequipped(InventoryItem item)
    {
        if (item?.IsEquipment == true) ScheduleCacheUpdate();
    }

    private void ScheduleCacheUpdate()
    {
        if (_updatePending) return;
        _updatePending = true;
        if (_debounceCoroutine != null)
            StopCoroutine(_debounceCoroutine);
        _debounceCoroutine = StartCoroutine(DebouncedCacheUpdate());
    }

    private IEnumerator DebouncedCacheUpdate()
    {
        yield return new WaitForSeconds(0.1f);
        try
        {
            var currentHero = PlayerSessionService.SelectedHero;
            if (currentHero == null)
            {
                Debug.LogWarning("[DataCacheService] No selected hero to update cache for");
                yield break;
            }
            CacheAttributesInternal(currentHero);
        }
        finally
        {
            _updatePending = false;
            _debounceCoroutine = null;
        }
    }

    // ─── Static wrappers (call sites unchanged) ──────────────────────────────

    private static string GetHeroKey(IHeroIdentity hero)
        => string.IsNullOrEmpty(hero.HeroName) ? hero.ClassId : hero.HeroName;

    public static void Clear()                                                          => Instance.ClearInternal();
    public static void CacheAttributes(HeroData heroData)                              => Instance.CacheAttributesInternal(heroData);
    public static HeroCalculatedAttributes GetHeroCalculatedAttributes(string heroId)  => Instance.GetHeroCalculatedAttributesInternal(heroId);
    public static void CacheFormations(string squadId, List<string> formations)        => Instance.CacheFormationsInternal(squadId, formations);
    public static List<string> GetUnlockedFormations(string squadId)                   => Instance.GetUnlockedFormationsInternal(squadId);
    public static void CachePerks(string heroId, List<string> perks)                   => Instance.CachePerksInternal(heroId, perks);
    public static List<string> GetActivePerks(string heroId)                           => Instance.GetActivePerksInternal(heroId);
    public static HeroCalculatedAttributes CalculateAttributes(HeroData heroData, EquipmentBonuses temporaryMods = default)
                                                                                        => CalculateAttributesInternal(heroData, temporaryMods);
    public static void InitializeEventListeners()                                       => Instance.InitializeEventListenersInternal();
    public static void CleanupEventListeners()                                          => Instance.CleanupEventListenersInternal();
}
