using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Provides cached access to dynamic hero data such as calculated attributes,
/// unlocked formations and active perks. This avoids recalculating values on
/// every scene load or gameplay tick.
/// </summary>
public static class DataCacheService
{
    // Cache of calculated attributes by hero identifier.
    static readonly Dictionary<string, HeroCalculatedAttributes> _attributeCache = new();

    // Cache of unlocked formation IDs per squad instance.
    static readonly Dictionary<string, List<string>> _formationCache = new();

    // Cache of active perk identifiers by hero.
    static readonly Dictionary<string, List<string>> _perkCache = new();

    // Equipment event listeners management
    private static bool _eventListenersInitialized = false;
    private static Coroutine _debounceCoroutine = null;
    private static bool _updatePending = false;

    #region Events

    /// <summary>
    /// Evento disparado cuando el cache de un héroe es actualizado.
    /// Útil para que la UI se refresque automáticamente.
    /// </summary>
    public static System.Action<string> OnHeroCacheUpdated;

    #endregion

    /// <summary>
    /// Clears all cached information. Useful when reloading player data.
    /// </summary>
    public static void Clear()
    {
        _attributeCache.Clear();
        _formationCache.Clear();
        _perkCache.Clear();
    }

    /// <summary>
    /// Calculates and stores the derived attributes for the given hero using new architecture.
    /// </summary>
    /// <param name="heroData">Hero progression data.</param>
    public static void CacheAttributes(HeroData heroData)
    {
        if (heroData == null) return;

        // Use new architecture for calculation - NO MORE LEGACY CONVERSION
        var newAttributes = CalculateAttributes(heroData);
        
        string key = GetHeroKey(heroData);
        _attributeCache[key] = newAttributes;

        // Disparar evento de actualización
        OnHeroCacheUpdated?.Invoke(key);
    }

    // Helper method for generating hero keys
    private static string GetHeroKey(HeroData heroData)
    {
        return string.IsNullOrEmpty(heroData.heroName) ? heroData.classId : heroData.heroName;
    }

    /// <summary>
    /// Retrieves previously cached hero calculated attributes.
    /// </summary>
    /// <param name="heroId">Unique identifier or name of the hero.</param>
    /// <returns>HeroCalculatedAttributes or Empty if not found</returns>
    public static HeroCalculatedAttributes GetHeroCalculatedAttributes(string heroId)
    {
        if (string.IsNullOrEmpty(heroId))
            return HeroCalculatedAttributes.Empty;
            
        _attributeCache.TryGetValue(heroId, out var attributes);
        return attributes; // Returns Empty if not found due to struct behavior
    }

    /// <summary>
    /// Stores the list of unlocked formation IDs for a squad instance.
    /// </summary>
    public static void CacheFormations(string squadId, List<string> formations)
    {
        if (string.IsNullOrEmpty(squadId) || formations == null)
            return;
        _formationCache[squadId] = new List<string>(formations);
    }

    /// <summary>
    /// Returns the cached formation IDs for the requested squad.
    /// </summary>
    public static List<string> GetUnlockedFormations(string squadId)
    {
        if (string.IsNullOrEmpty(squadId))
            return null;
        _formationCache.TryGetValue(squadId, out var list);
        return list;
    }

    /// <summary>
    /// Stores the list of currently active perk IDs for a hero.
    /// </summary>
    public static void CachePerks(string heroId, List<string> perks)
    {
        if (string.IsNullOrEmpty(heroId) || perks == null)
            return;
        _perkCache[heroId] = new List<string>(perks);
    }

    /// <summary>
    /// Returns the cached active perk IDs for the requested hero.
    /// </summary>
    public static List<string> GetActivePerks(string heroId)
    {
        if (string.IsNullOrEmpty(heroId))
            return null;
        _perkCache.TryGetValue(heroId, out var list);
        return list;
    }

    #region Attribute Calculation System

    /// <summary>
    /// Extrae los stats base de un HeroData sin modificaciones.
    /// </summary>
    /// <param name="heroData">Datos del héroe</param>
    /// <returns>Estructura con los stats base puros</returns>
    public static HeroBaseStats GetBaseStats(HeroData heroData)
    {
        return HeroBaseStats.FromHeroData(heroData);
    }

    /// <summary>
    /// Calcula las bonificaciones de equipamiento actuales.
    /// </summary>
    /// <returns>Estructura con bonificaciones de equipamiento</returns>
    public static EquipmentBonuses GetEquipmentBonuses()
    {
        var equipmentStats = EquipmentManagerService.CalculateTotalEquipmentStats();
        
        return new EquipmentBonuses
        {
            strengthBonus = equipmentStats.ContainsKey("Strength") ? (int)equipmentStats["Strength"] : 0,
            dexterityBonus = equipmentStats.ContainsKey("Dexterity") ? (int)equipmentStats["Dexterity"] : 0,
            armorBonus = equipmentStats.ContainsKey("Armor") ? (int)equipmentStats["Armor"] : 0,
            vitalityBonus = equipmentStats.ContainsKey("Vitality") ? (int)equipmentStats["Vitality"] : 0
        };
    }

    /// <summary>
    /// Calcula los atributos finales usando la nueva arquitectura separada.
    /// </summary>
    /// <param name="heroData">Datos base del héroe</param>
    /// <param name="temporaryMods">Modificaciones temporales (opcional)</param>
    /// <returns>Atributos completamente calculados</returns>
    public static HeroCalculatedAttributes CalculateAttributes(HeroData heroData, EquipmentBonuses temporaryMods = default)
    {
        if (heroData == null) return HeroCalculatedAttributes.Empty;

        var baseStats = GetBaseStats(heroData);
        var equipmentBonuses = GetEquipmentBonuses();
        var classDefinition = HeroClassManager.GetClassDefinition(heroData.classId);

        return HeroCalculatedAttributes.Calculate(baseStats, equipmentBonuses, temporaryMods, classDefinition);
    }

    #region Clear Naming Methods for Attribute Values

    /// <summary>
    /// Obtiene el valor base puro (solo HeroData) de un atributo específico.
    /// </summary>
    /// <param name="heroData">Datos del héroe</param>
    /// <param name="attributeName">Nombre del atributo</param>
    /// <returns>Valor base puro sin modificaciones</returns>
    public static float GetPureBaseValue(HeroData heroData, string attributeName)
    {
        if (heroData == null || string.IsNullOrEmpty(attributeName))
            return 0f;

        switch (attributeName.ToLower())
        {
            case "strength": return heroData.strength;
            case "dexterity": return heroData.dexterity;
            case "armor": return heroData.armor;
            case "vitality": return heroData.vitality;
            default:
                Debug.LogWarning($"[DataCacheService] Atributo no reconocido: {attributeName}");
                return 0f;
        }
    }

    /// <summary>
    /// Obtiene la bonificación de equipamiento para un atributo específico.
    /// </summary>
    /// <param name="heroData">Datos del héroe</param>
    /// <param name="attributeName">Nombre del atributo</param>
    /// <returns>Bonificación del equipamiento</returns>
    public static float GetEquipmentBonusValue(HeroData heroData, string attributeName)
    {
        if (heroData == null || string.IsNullOrEmpty(attributeName))
            return 0f;

        var equipmentBonuses = GetEquipmentBonuses();
        
        switch (attributeName.ToLower())
        {
            case "strength": return equipmentBonuses.strengthBonus;
            case "dexterity": return equipmentBonuses.dexterityBonus;
            case "armor": return equipmentBonuses.armorBonus;
            case "vitality": return equipmentBonuses.vitalityBonus;
            default:
                Debug.LogWarning($"[DataCacheService] Atributo no reconocido: {attributeName}");
                return 0f;
        }
    }

    /// <summary>
    /// Obtiene el valor que se muestra normalmente en UI (Base + Equipamiento).
    /// Este es el valor que el usuario ve antes de hacer modificaciones temporales.
    /// </summary>
    /// <param name="heroData">Datos del héroe</param>
    /// <param name="attributeName">Nombre del atributo</param>
    /// <returns>Base + Equipamiento</returns>
    public static float GetBaseWithEquipment(HeroData heroData, string attributeName)
    {
        return GetPureBaseValue(heroData, attributeName) + GetEquipmentBonusValue(heroData, attributeName);
    }

    /// <summary>
    /// Obtiene el valor final calculado incluyendo todas las modificaciones temporales.
    /// Este es el valor que se muestra cuando hay cambios temporales activos.
    /// </summary>
    /// <param name="heroData">Datos del héroe</param>
    /// <param name="attributeName">Nombre del atributo</param>
    /// <param name="temporaryMods">Modificaciones temporales</param>
    /// <returns>Base + Equipamiento + Temporales</returns>
    public static float GetFinalCalculatedValue(HeroData heroData, string attributeName, EquipmentBonuses temporaryMods = default)
    {
        var baseWithEquipment = GetBaseWithEquipment(heroData, attributeName);
        
        if (!temporaryMods.HasBonuses)
            return baseWithEquipment;

        float temporaryBonus = 0f;
        switch (attributeName.ToLower())
        {
            case "strength": temporaryBonus = temporaryMods.strengthBonus; break;
            case "dexterity": temporaryBonus = temporaryMods.dexterityBonus; break;
            case "armor": temporaryBonus = temporaryMods.armorBonus; break;
            case "vitality": temporaryBonus = temporaryMods.vitalityBonus; break;
        }

        return baseWithEquipment + temporaryBonus;
    }

    #endregion
    #endregion

    #region Equipment Event Listeners

    /// <summary>
    /// Inicializa los listeners para eventos de equipamiento.
    /// Debe llamarse cuando se selecciona un héroe activo.
    /// </summary>
    public static void InitializeEventListeners()
    {
        if (_eventListenersInitialized) return;

        try
        {
            InventoryEventService.OnItemEquipped += OnItemEquipped;
            InventoryEventService.OnItemUnequipped += OnItemUnequipped;
            _eventListenersInitialized = true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DataCacheService] Failed to initialize event listeners: {e.Message}");
        }
    }

    /// <summary>
    /// Limpia los listeners de eventos de equipamiento.
    /// Debe llamarse al cambiar de héroe o cerrar sesión.
    /// </summary>
    public static void CleanupEventListeners()
    {
        if (!_eventListenersInitialized) return;

        try
        {
            InventoryEventService.OnItemEquipped -= OnItemEquipped;
            InventoryEventService.OnItemUnequipped -= OnItemUnequipped;
            _eventListenersInitialized = false;
            
            // Cancelar debounce pendiente
            if (_debounceCoroutine != null)
            {
                MonoBehaviourHelper.StopCoroutineIfRunning(_debounceCoroutine);
                _debounceCoroutine = null;
                _updatePending = false;
            }            
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DataCacheService] Failed to cleanup event listeners: {e.Message}");
        }
    }

    /// <summary>
    /// Maneja el evento de item equipado.
    /// </summary>
    private static void OnItemEquipped(InventoryItem equippedItem, InventoryItem unequippedItem)
    {
        if (equippedItem?.IsEquipment == true) ScheduleCacheUpdate();
    }

    /// <summary>
    /// Maneja el evento de item desequipado.
    /// </summary>
    private static void OnItemUnequipped(InventoryItem item)
    {
        if (item?.IsEquipment == true) ScheduleCacheUpdate();
    }

    /// <summary>
    /// Programa una actualización del cache con debounce.
    /// Evita múltiples recalculaciones cuando se hacen varios cambios rápidos.
    /// </summary>
    private static void ScheduleCacheUpdate()
    {
        if (_updatePending) return;
        
        _updatePending = true;
        
        // Cancelar corrutina anterior si existe
        if (_debounceCoroutine != null)
        {
            MonoBehaviourHelper.StopCoroutineIfRunning(_debounceCoroutine);
        }
        
        // Iniciar nueva corrutina con debounce
        _debounceCoroutine = MonoBehaviourHelper.StartCoroutine(DebouncedCacheUpdate());
    }

    /// <summary>
    /// Corrutina que actualiza el cache después de un pequeño delay.
    /// </summary>
    private static IEnumerator DebouncedCacheUpdate()
    {
        yield return new WaitForSeconds(0.1f); // 100ms debounce
        
        try
        {
            UpdateCurrentHeroCache();
        }
        finally
        {
            _updatePending = false;
            _debounceCoroutine = null;
        }
    }

    /// <summary>
    /// Actualiza el cache del héroe actualmente seleccionado.
    /// Optimización: solo actualiza el héroe específico, no todos.
    /// </summary>
    private static void UpdateCurrentHeroCache()
    {
        var currentHero = PlayerSessionService.SelectedHero;
        if (currentHero == null)
        {
            Debug.LogWarning("[DataCacheService] No selected hero to update cache for");
            return;
        }
        
        CacheAttributes(currentHero);
    }

    #endregion

    #region MonoBehaviour Helper

    /// <summary>
    /// Helper estático para manejar corrutinas desde una clase estática.
    /// </summary>
    private static class MonoBehaviourHelper
    {
        private static MonoBehaviour _instance;
        
        private static MonoBehaviour Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[DataCacheService Helper]");
                    _instance = go.AddComponent<DataCacheServiceHelper>();
                    Object.DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }
        
        public static Coroutine StartCoroutine(IEnumerator routine)
        {
            return Instance.StartCoroutine(routine);
        }
        
        public static void StopCoroutineIfRunning(Coroutine coroutine)
        {
            if (coroutine != null && Instance != null)
            {
                Instance.StopCoroutine(coroutine);
            }
        }
    }

    /// <summary>
    /// MonoBehaviour helper para ejecutar corrutinas.
    /// </summary>
    private class DataCacheServiceHelper : MonoBehaviour
    {
        private void OnDestroy()
        {
            // Limpiar referencias cuando se destruye
            CleanupEventListeners();
        }
    }

    #endregion
}
