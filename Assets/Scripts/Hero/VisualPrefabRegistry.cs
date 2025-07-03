using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton que mantiene un registro de prefabs visuales disponibles
/// para la instanciación híbrida. Permite acceso fácil desde los sistemas ECS.
/// </summary>
public class VisualPrefabRegistry : MonoBehaviour
{
    [Header("Hero Visual Prefabs")]
    public GameObject heroSyntyPrefab;
    public GameObject heroAlternatePrefab;
    
    [Header("Other Visual Prefabs")]
    public GameObject[] additionalPrefabs;
    
    private static VisualPrefabRegistry _instance;
    private Dictionary<string, GameObject> _prefabDictionary;
    
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
            InitializePrefabDictionary();
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// Inicializa el diccionario de prefabs para búsqueda rápida.
    /// </summary>
    private void InitializePrefabDictionary()
    {
        _prefabDictionary = new Dictionary<string, GameObject>();
        int uniquePrefabCount = 0;
        
        if (heroSyntyPrefab != null)
        {
            _prefabDictionary["HeroSynty"] = heroSyntyPrefab;
            // Solo registrar con un nombre para evitar confusión
            uniquePrefabCount++;
            Debug.Log($"[VisualPrefabRegistry] Registrado prefab 'HeroSynty': {heroSyntyPrefab.name}");
        }
        
        if (heroAlternatePrefab != null)
        {
            _prefabDictionary["HeroAlternate"] = heroAlternatePrefab;
            uniquePrefabCount++;
            Debug.Log($"[VisualPrefabRegistry] Registrado prefab 'HeroAlternate': {heroAlternatePrefab.name}");
        }
        
        // Registrar prefabs adicionales
        foreach (var prefab in additionalPrefabs)
        {
            if (prefab != null)
            {
                _prefabDictionary[prefab.name] = prefab;
                uniquePrefabCount++;
                Debug.Log($"[VisualPrefabRegistry] Registrado prefab adicional: {prefab.name}");
            }
        }
        
        Debug.Log($"[VisualPrefabRegistry] Registrados {uniquePrefabCount} prefabs únicos con {_prefabDictionary.Count} entradas de diccionario");
    }
    
    /// <summary>
    /// Obtiene un prefab visual por su nombre/clave.
    /// </summary>
    /// <param name="prefabKey">Clave o nombre del prefab</param>
    /// <returns>GameObject del prefab o null si no se encuentra</returns>
    public GameObject GetPrefab(string prefabKey)
    {
        if (_prefabDictionary.TryGetValue(prefabKey, out GameObject prefab))
        {
            return prefab;
        }
        
        Debug.LogWarning($"[VisualPrefabRegistry] Prefab visual '{prefabKey}' no encontrado");
        return null;
    }
    
    /// <summary>
    /// Registra un nuevo prefab en tiempo de ejecución.
    /// </summary>
    /// <param name="key">Clave para el prefab</param>
    /// <param name="prefab">GameObject del prefab</param>
    public void RegisterPrefab(string key, GameObject prefab)
    {
        if (_prefabDictionary == null)
        {
            InitializePrefabDictionary();
        }
        
        _prefabDictionary[key] = prefab;
        Debug.Log($"[VisualPrefabRegistry] Registrado prefab '{key}'");
    }
    
    /// <summary>
    /// Obtiene el prefab visual por defecto del héroe.
    /// </summary>
    /// <returns>GameObject del prefab del héroe o null</returns>
    public GameObject GetDefaultHeroPrefab()
    {
        return GetPrefab("HeroSynty") ?? GetPrefab("HeroVisual_Synty");
    }
}
