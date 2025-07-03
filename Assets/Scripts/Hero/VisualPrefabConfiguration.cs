using UnityEngine;

/// <summary>
/// ScriptableObject que define la configuración de prefabs visuales para diferentes tipos de unidades.
/// Permite agregar nuevos tipos de unidades sin modificar código.
/// </summary>
[CreateAssetMenu(fileName = "VisualPrefabConfig", menuName = "Conquest Tactics/Visual Prefab Configuration")]
public class VisualPrefabConfiguration : ScriptableObject
{
    [Header("Hero Visual Prefabs")]
    [SerializeField] private HeroPrefabEntry[] heroPrefabs;
    
    [Header("Unit Visual Prefabs")]
    [SerializeField] private UnitPrefabEntry[] unitPrefabs;
    
    [Header("Additional Visual Prefabs")]
    [SerializeField] private GenericPrefabEntry[] additionalPrefabs;
    
    /// <summary>
    /// Entrada de prefab para héroes.
    /// </summary>
    [System.Serializable]
    public class HeroPrefabEntry
    {
        [Tooltip("Identificador único del héroe")]
        public string heroId;
        
        [Tooltip("Nombre descriptivo del héroe")]
        public string displayName;
        
        [Tooltip("Prefab visual del héroe")]
        public GameObject visualPrefab;
        
        [Tooltip("Es el prefab por defecto")]
        public bool isDefault;
    }
    
    /// <summary>
    /// Entrada de prefab para unidades.
    /// </summary>
    [System.Serializable]
    public class UnitPrefabEntry
    {
        [Tooltip("Tipo de escuadrón al que pertenece")]
        public SquadType squadType;
        
        [Tooltip("Identificador único de la unidad")]
        public string unitId;
        
        [Tooltip("Nombre descriptivo de la unidad")]
        public string displayName;
        
        [Tooltip("Prefab visual de la unidad")]
        public GameObject visualPrefab;
        
        [Tooltip("Es el prefab por defecto para este tipo de escuadrón")]
        public bool isDefault;
        
        [Tooltip("Variantes del mismo tipo de unidad (ej: diferentes armaduras)")]
        public GameObject[] variants;
    }
    
    /// <summary>
    /// Entrada de prefab genérico.
    /// </summary>
    [System.Serializable]
    public class GenericPrefabEntry
    {
        [Tooltip("Clave/ID del prefab")]
        public string prefabKey;
        
        [Tooltip("Prefab GameObject")]
        public GameObject prefab;
        
        [Tooltip("Descripción del prefab")]
        public string description;
    }
    
    // Getters públicos para acceso desde el Registry
    public HeroPrefabEntry[] HeroPrefabs => heroPrefabs;
    public UnitPrefabEntry[] UnitPrefabs => unitPrefabs;
    public GenericPrefabEntry[] AdditionalPrefabs => additionalPrefabs;
    
    /// <summary>
    /// Obtiene un prefab de héroe por ID.
    /// </summary>
    public GameObject GetHeroPrefab(string heroId)
    {
        foreach (var entry in heroPrefabs)
        {
            if (entry.heroId == heroId && entry.visualPrefab != null)
                return entry.visualPrefab;
        }
        return null;
    }
    
    /// <summary>
    /// Obtiene el prefab de héroe por defecto.
    /// </summary>
    public GameObject GetDefaultHeroPrefab()
    {
        foreach (var entry in heroPrefabs)
        {
            if (entry.isDefault && entry.visualPrefab != null)
                return entry.visualPrefab;
        }
        
        // Fallback al primer héroe disponible
        return heroPrefabs.Length > 0 ? heroPrefabs[0].visualPrefab : null;
    }
    
    /// <summary>
    /// Obtiene un prefab de unidad por tipo de escuadrón y ID.
    /// </summary>
    public GameObject GetUnitPrefab(SquadType squadType, string unitId = null)
    {
        foreach (var entry in unitPrefabs)
        {
            if (entry.squadType == squadType)
            {
                // Si se especifica unitId, buscar exacto
                if (!string.IsNullOrEmpty(unitId) && entry.unitId == unitId)
                    return entry.visualPrefab;
                
                // Si no se especifica unitId, devolver el default de ese tipo
                if (string.IsNullOrEmpty(unitId) && entry.isDefault)
                    return entry.visualPrefab;
            }
        }
        
        // Fallback: primer prefab del tipo
        foreach (var entry in unitPrefabs)
        {
            if (entry.squadType == squadType && entry.visualPrefab != null)
                return entry.visualPrefab;
        }
        
        return null;
    }
    
    /// <summary>
    /// Obtiene todas las variantes de una unidad.
    /// </summary>
    public GameObject[] GetUnitVariants(SquadType squadType, string unitId)
    {
        foreach (var entry in unitPrefabs)
        {
            if (entry.squadType == squadType && entry.unitId == unitId)
                return entry.variants ?? new GameObject[0];
        }
        return new GameObject[0];
    }
    
    /// <summary>
    /// Valida la configuración y reporta errores.
    /// </summary>
    public void ValidateConfiguration()
    {
        // Validar héroes
        bool hasDefaultHero = false;
        foreach (var hero in heroPrefabs)
        {
            if (hero.isDefault)
            {
                if (hasDefaultHero)
                    Debug.LogWarning($"[VisualPrefabConfiguration] Múltiples héroes marcados como default: {hero.heroId}");
                hasDefaultHero = true;
            }
            
            if (hero.visualPrefab == null)
                Debug.LogError($"[VisualPrefabConfiguration] Héroe '{hero.heroId}' no tiene prefab asignado");
        }
        
        if (!hasDefaultHero && heroPrefabs.Length > 0)
            Debug.LogWarning("[VisualPrefabConfiguration] Ningún héroe marcado como default");
        
        // Validar unidades por tipo
        foreach (SquadType squadType in System.Enum.GetValues(typeof(SquadType)))
        {
            bool hasDefaultUnit = false;
            int unitCount = 0;
            
            foreach (var unit in unitPrefabs)
            {
                if (unit.squadType == squadType)
                {
                    unitCount++;
                    if (unit.isDefault)
                    {
                        if (hasDefaultUnit)
                            Debug.LogWarning($"[VisualPrefabConfiguration] Múltiples unidades default para {squadType}: {unit.unitId}");
                        hasDefaultUnit = true;
                    }
                    
                    if (unit.visualPrefab == null)
                        Debug.LogError($"[VisualPrefabConfiguration] Unidad '{unit.unitId}' ({squadType}) no tiene prefab asignado");
                }
            }
            
            if (unitCount > 0 && !hasDefaultUnit)
                Debug.LogWarning($"[VisualPrefabConfiguration] Ninguna unidad default para {squadType}");
        }
    }
}
