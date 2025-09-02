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
    static readonly Dictionary<string, CalculatedAttributes> _attributeCache = new();

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
    /// Calculates and stores the derived attributes for the given hero.
    /// </summary>
    /// <param name="heroData">Hero progression data.</param>
    public static void CacheAttributes(HeroData heroData)
    {
        if (heroData == null) return;

        var calculated = CalculateAttributes(heroData);
        string key = GetHeroKey(heroData);
        _attributeCache[key] = calculated;

        // Disparar evento de actualización
        OnHeroCacheUpdated?.Invoke(key);
    }

    /// <summary>
    /// Retrieves previously cached attributes for a hero.
    /// </summary>
    /// <param name="heroId">Unique identifier or name of the hero.</param>
    public static CalculatedAttributes GetCachedAttributes(string heroId)
    {
        if (string.IsNullOrEmpty(heroId))
            return null;
        _attributeCache.TryGetValue(heroId, out var attributes);
        return attributes;
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

    // Recalcula los atributos cacheados para todos los héroes del jugador.
    public static void RecalculateAttributes(HeroData hero)
    {
        Clear();
        if (hero == null) return;
        CacheAttributes(hero);
        
    }

    // Helper --------------------------------------------------------------

    // Generates a unique key for the hero based on available data.
    static string GetHeroKey(HeroData heroData)
    {
        // If the project later introduces an explicit ID use it here.
        return string.IsNullOrEmpty(heroData.heroName) ? heroData.classId : heroData.heroName;
    }

    /// <summary>
    /// Performs the actual attribute calculations using formulas from the GDD.
    /// Public method that can be used by other services for consistent calculations.
    /// </summary>
    public static CalculatedAttributes CalculateAttributes(HeroData hero)
    {
        // Get class definition for constants
        var classData = HeroClassManager.GetClassDefinition(hero.classId);
        if (classData == null) Debug.LogWarning($"[DataCacheService] No class definition found for {hero.classId}, using defaults");

        // Get equipment stats bonuses
        var equipmentStats = EquipmentManagerService.CalculateTotalEquipmentStats();
        // Primary attributes with equipment bonuses
        float strength = hero.strength + (equipmentStats.ContainsKey("Strength") ? equipmentStats["Strength"] : 0);
        float dexterity = hero.dexterity + (equipmentStats.ContainsKey("Dexterity") ? equipmentStats["Dexterity"] : 0);
        float armor = hero.armor + (equipmentStats.ContainsKey("Armor") ? equipmentStats["Armor"] : 0);
        float vitality = hero.vitality + (equipmentStats.ContainsKey("Vitality") ? equipmentStats["Vitality"] : 0);

        return CalculateDerivedAttributes(classData, strength, dexterity, armor, vitality, HeroLeadershipCalculator.CalculateLeadership(hero));
    }

    /// <summary>
    /// Sobrecarga para calcular atributos derivados usando valores específicos de atributos base.
    /// Útil para HeroTempAttributeService cuando se calculan modificaciones temporales.
    /// </summary>
    /// <param name="classData">Definición de clase del héroe</param>
    /// <param name="strength">Valor de fuerza (con modificaciones temporales aplicadas)</param>
    /// <param name="dexterity">Valor de destreza (con modificaciones temporales aplicadas)</param>
    /// <param name="armor">Valor de armadura (con modificaciones temporales aplicadas)</param>
    /// <param name="vitality">Valor de vitalidad (con modificaciones temporales aplicadas)</param>
    /// <param name="leadership">Valor de liderazgo (con modificaciones temporales aplicadas)</param>
    /// <returns>CalculatedAttributes con valores derivados calculados</returns>
    public static CalculatedAttributes CalculateDerivedAttributes(HeroClassDefinition classData,
        float strength, float dexterity, float armor, float vitality, float leadership)
    {
        var result = new CalculatedAttributes();

        if (classData == null)
        {
            Debug.LogWarning("[DataCacheService] No class definition provided for derived calculations");
            return result;
        }

        // Base values from class definition
        float baseHealth = classData.baseHealth;
        float baseStamina = classData.baseStamina;
        float baseDamage = classData.baseDamage;
        float baseArmor = classData.baseArmorValue;

        // Set primary attributes
        result.strength = strength;
        result.dexterity = dexterity;
        result.vitality = vitality;
        result.armor = armor;
        result.leadership = leadership;

        // Calculate derived stats using the same formulas
        result.maxHealth = baseHealth + (classData.healthPerVitality * vitality);
        result.stamina = baseStamina + (classData.staminaPerDexterity * dexterity);

        result.bluntDamage = baseDamage + (classData.bluntDamageMultiplierByStr * strength);
        result.slashingDamage = baseDamage + (classData.slashingDamageMultiplierByStr * strength + classData.slashingDamageMultiplierByDex * dexterity);
        result.piercingDamage = baseDamage + (classData.piercingDamageMultiplierByDex * dexterity);

        result.bluntDefense = baseArmor + (armor * classData.bluntDefenseMultiplierByArmor);
        result.slashDefense = baseArmor + (armor * classData.slashingDefenseMultiplierByArmor);
        result.pierceDefense = baseArmor + (armor * classData.piercingDefenseMultiplierByArmor);

        result.bluntPenetration = strength * classData.bluntPenetrationMultiplierByStr;
        result.slashPenetration = (strength * classData.slashingPenetrationMultiplierByStr) + (dexterity * classData.slashingPenetrationMultiplierByDex);
        result.piercePenetration = dexterity * classData.piercingPenetrationMultiplierByDex;

        result.blockPower = strength * classData.blockPowerMultiplierByStr;
        result.movementSpeed = classData.movementSpeedBase + dexterity * classData.movementSpeedDexMultiplier;

        return result;
    }

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
