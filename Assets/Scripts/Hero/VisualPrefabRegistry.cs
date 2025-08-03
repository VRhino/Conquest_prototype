using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton que mantiene un registro de prefabs visuales disponibles
/// para la instanciación híbrida. Permite acceso fácil desde los sistemas ECS.
/// Ahora usa VisualPrefabConfiguration para soporte escalable de N unidades.
/// </summary>
[System.Serializable]
public class VisualPrefabRegistry : MonoBehaviour
{
    [Header("Visual Prefab Configuration")]
    [SerializeField] private VisualPrefabConfiguration config;
    
    [Header("Runtime Configuration")]
    [SerializeField] private bool autoValidateOnStart = true;
    
    private static VisualPrefabRegistry _instance;
    private Dictionary<string, GameObject> _runtimePrefabCache;
    
    public static VisualPrefabRegistry Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<VisualPrefabRegistry>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("VisualPrefabRegistry");
                    _instance = go.AddComponent<VisualPrefabRegistry>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }
    
    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            InitializePrefabCache();
            
            if (autoValidateOnStart && config != null)
            {
                config.ValidateConfiguration();
            }
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// Inicializa el cache de prefabs para búsqueda rápida en runtime.
    /// </summary>
    private void InitializePrefabCache()
    {
        _runtimePrefabCache = new Dictionary<string, GameObject>();
        
        if (config == null)
        {
            Debug.LogError("[VisualPrefabRegistry] No se ha asignado VisualPrefabConfiguration");
            return;
        }
        
        int totalPrefabs = 0;
        
        // Cache hero prefabs
        foreach (var heroPrefab in config.HeroPrefabs)
        {
            if (heroPrefab.visualPrefab != null)
            {
                string key = heroPrefab.heroId;
                _runtimePrefabCache[key] = heroPrefab.visualPrefab;
                
                // También agregar con clave legacy para compatibilidad
                if (heroPrefab.heroId == "Synty")
                    _runtimePrefabCache["HeroSynty"] = heroPrefab.visualPrefab;
                
                totalPrefabs++;
                Debug.Log($"[VisualPrefabRegistry] Cached hero prefab: {key}");
            }
        }
        
        // Cache unit prefabs
        foreach (var unitPrefab in config.UnitPrefabs)
        {
            if (unitPrefab.visualPrefab != null)
            {
                string key = $"{unitPrefab.unitId}";
                _runtimePrefabCache[key] = unitPrefab.visualPrefab;
                
                // También agregar claves legacy para compatibilidad
                string legacyKey = GetLegacyUnitKey(unitPrefab.squadType);
                if (!string.IsNullOrEmpty(legacyKey))
                    _runtimePrefabCache[legacyKey] = unitPrefab.visualPrefab;
                
                totalPrefabs++;
                Debug.Log($"[VisualPrefabRegistry] Cached unit prefab: {key}");
            }
        }
        
        // Cache additional prefabs
        foreach (var additionalPrefab in config.AdditionalPrefabs)
        {
            if (additionalPrefab.prefab != null)
            {
                _runtimePrefabCache[additionalPrefab.prefabKey] = additionalPrefab.prefab;
                totalPrefabs++;
                Debug.Log($"[VisualPrefabRegistry] Cached additional prefab: {additionalPrefab.prefabKey}");
            }
        }
        
        Debug.Log($"[VisualPrefabRegistry] Initialized with {totalPrefabs} prefabs total");
    }
    
    /// <summary>
    /// Obtiene clave legacy para compatibilidad con código existente.
    /// </summary>
    private string GetLegacyUnitKey(SquadType squadType)
    {
        return squadType switch
        {
            SquadType.Squires => "UnitEscudero",
            SquadType.Archers => "UnitArquero", 
            SquadType.Pikemen => "UnitPikemen",
            SquadType.Spearmen => "UnitSpearmen",
            _ => null
        };
    }
    
    /// <summary>
    /// Obtiene un prefab visual por su clave.
    /// </summary>
    /// <param name="prefabKey">Clave del prefab</param>
    /// <returns>GameObject del prefab o null si no se encuentra</returns>
    public GameObject GetPrefab(string prefabKey)
    {
        if (_runtimePrefabCache != null && _runtimePrefabCache.TryGetValue(prefabKey, out GameObject prefab))
        {
            return prefab;
        }
        
        Debug.LogWarning($"[VisualPrefabRegistry] Prefab visual '{prefabKey}' no encontrado");
        return null;
    }
    
    /// <summary>
    /// Obtiene el prefab visual por defecto del héroe.
    /// </summary>
    /// <returns>GameObject del prefab del héroe o null</returns>
    public GameObject GetDefaultHeroPrefab()
    {
        if (config == null) return null;
        return config.GetDefaultHeroPrefab();
    }
    
    /// <summary>
    /// Obtiene un prefab de héroe específico por ID.
    /// </summary>
    /// <param name="heroId">ID del héroe</param>
    /// <returns>GameObject del prefab del héroe o null</returns>
    public GameObject GetHeroPrefab(string heroId)
    {
        if (config == null) return null;
        return config.GetHeroPrefab(heroId);
    }
    
    /// <summary>
    /// Obtiene el prefab visual por defecto de unidad según el tipo.
    /// </summary>
    /// <param name="squadType">Tipo de squad</param>
    /// <returns>GameObject de la unidad por defecto</returns>
    public GameObject GetDefaultUnitPrefab(SquadType squadType = SquadType.Squires)
    {
        if (config == null) return null;
        return config.GetUnitPrefab(squadType);
    }
    
    /// <summary>
    /// Obtiene un prefab de unidad específico.
    /// </summary>
    /// <param name="squadType">Tipo de squad</param>
    /// <param name="unitId">ID específico de la unidad (opcional)</param>
    /// <returns>GameObject del prefab de la unidad</returns>
    public GameObject GetUnitPrefab(SquadType squadType, string unitId = null)
    {
        if (config == null) return null;
        return config.GetUnitPrefab(squadType, unitId);
    }
    
    /// <summary>
    /// Obtiene todas las variantes disponibles de una unidad.
    /// </summary>
    /// <param name="squadType">Tipo de squad</param>
    /// <param name="unitId">ID de la unidad</param>
    /// <returns>Array de GameObjects con las variantes</returns>
    public GameObject[] GetUnitVariants(SquadType squadType, string unitId)
    {
        if (config == null) return new GameObject[0];
        return config.GetUnitVariants(squadType, unitId);
    }
    
    /// <summary>
    /// Registra un nuevo prefab en tiempo de ejecución.
    /// </summary>
    /// <param name="key">Clave para el prefab</param>
    /// <param name="prefab">GameObject del prefab</param>
    public void RegisterPrefab(string key, GameObject prefab)
    {
        if (_runtimePrefabCache == null)
        {
            InitializePrefabCache();
        }
        
        _runtimePrefabCache[key] = prefab;
        Debug.Log($"[VisualPrefabRegistry] Registered runtime prefab: {key}");
    }
    
    /// <summary>
    /// Obtiene todos los tipos de escuadrón que tienen prefabs configurados.
    /// </summary>
    /// <returns>Array de SquadTypes disponibles</returns>
    public SquadType[] GetAvailableSquadTypes()
    {
        if (config == null) return new SquadType[0];
        
        var availableTypes = new System.Collections.Generic.HashSet<SquadType>();
        foreach (var unit in config.UnitPrefabs)
        {
            if (unit.visualPrefab != null)
                availableTypes.Add(unit.squadType);
        }
        
        var result = new SquadType[availableTypes.Count];
        availableTypes.CopyTo(result);
        return result;
    }
    
    /// <summary>
    /// Obtiene todos los IDs de unidad disponibles para un tipo de escuadrón.
    /// </summary>
    /// <param name="squadType">Tipo de escuadrón</param>
    /// <returns>Array de IDs de unidad</returns>
    public string[] GetAvailableUnitIds(SquadType squadType)
    {
        if (config == null) return new string[0];
        
        var unitIds = new System.Collections.Generic.List<string>();
        foreach (var unit in config.UnitPrefabs)
        {
            if (unit.squadType == squadType && unit.visualPrefab != null)
                unitIds.Add(unit.unitId);
        }
        
        return unitIds.ToArray();
    }
    
    /// <summary>
    /// Valida la configuración actual y reporta errores.
    /// </summary>
    public void ValidateConfiguration()
    {
        if (config == null)
        {
            Debug.LogError("[VisualPrefabRegistry] No se ha asignado VisualPrefabConfiguration");
            return;
        }
        
        config.ValidateConfiguration();
    }
}
