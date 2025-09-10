using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Servicio centralizado para acceso a datos de squads.
/// Maneja caché inteligente y proporciona API unificada para todas las operaciones de SquadData.
/// </summary>
public static class SquadDataService
{
    #region Private Fields
    
    private static SquadDatabase _squadDatabase;
    private static Dictionary<string, SquadData> _squadCache;
    private static Dictionary<UnitType, List<SquadData>> _squadsByType;
    private static bool _isInitialized = false;
    
    #endregion
    
    #region Initialization
    
    /// <summary>
    /// Inicializa el servicio cargando la base de datos y preparando los caches.
    /// </summary>
    public static void Initialize()
    {
        if (_isInitialized) return;
        
        LoadSquadDatabase();
        BuildCaches();
        _isInitialized = true;
        
        Debug.Log($"[SquadDataService] Inicializado con {_squadCache.Count} squads");
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
        _squadDatabase = null;
        _squadCache?.Clear();
        _squadsByType?.Clear();
        Initialize();
    }
    
    #endregion
    
    #region Core API
    
    /// <summary>
    /// Obtiene un SquadData por su ID.
    /// </summary>
    /// <param name="squadId">ID único del squad</param>
    /// <returns>SquadData encontrado o null si no existe</returns>
    public static SquadData GetSquadById(string squadId)
    {
        EnsureInitialized();
        
        if (string.IsNullOrEmpty(squadId))
            return null;
            
        _squadCache.TryGetValue(squadId, out SquadData squadData);
        return squadData;
    }
    
    /// <summary>
    /// Obtiene todos los squads de un tipo específico.
    /// </summary>
    /// <param name="unitType">Tipo de unidad a filtrar</param>
    /// <returns>Lista de squads del tipo especificado</returns>
    public static List<SquadData> GetSquadsByType(UnitType unitType)
    {
        EnsureInitialized();
        
        if (_squadsByType.TryGetValue(unitType, out List<SquadData> squads))
            return new List<SquadData>(squads);
            
        return new List<SquadData>();
    }
    
    /// <summary>
    /// Obtiene todos los squads disponibles.
    /// </summary>
    /// <returns>Lista completa de todos los squads</returns>
    public static List<SquadData> GetAllSquads()
    {
        EnsureInitialized();
        return new List<SquadData>(_squadCache.Values);
    }
    
    /// <summary>
    /// Obtiene squads disponibles para un héroe, opcionalmente filtrados por tipo.
    /// </summary>
    /// <param name="availableSquads">Lista de IDs de squads disponibles para el héroe</param>
    /// <param name="filterType">Tipo opcional para filtrar</param>
    /// <returns>Lista de SquadData disponibles</returns>
    public static List<SquadData> GetSquadsForHero(List<string> availableSquads, UnitType? filterType = null)
    {
        EnsureInitialized();
        
        if (availableSquads == null || availableSquads.Count == 0)
            return new List<SquadData>();
        
        var result = new List<SquadData>();
        
        foreach (string squadId in availableSquads)
        {
            var squadData = GetSquadById(squadId);
            if (squadData == null) continue;
            
            // Aplicar filtro de tipo si se especifica
            if (filterType.HasValue && squadData.unitType != filterType.Value)
                continue;
                
            result.Add(squadData);
        }
        
        return result;
    }

    #endregion

    #region Conversion API for BattlePreparation

    /// <summary>
    /// Convierte un squad ID a SquadIconData para uso en UI.
    /// </summary>
    /// <param name="squadId">ID del squad</param>
    /// <returns>SquadIconData o null si no se encuentra</returns>
    public static SquadIconData ConvertToSquadIconData(string squadId)
    {
        var squadData = GetSquadById(squadId);
        if (squadData == null) return null;

        return new SquadIconData(
            squadId: squadData.id,
            backgroundSprite: squadData.background,
            iconSprite: squadData.icon,
            underlineColor: SquadUtils.GetRarityColor(squadData.rarity)
        );
    }
    
    /// <summary>
    /// Convierte una lista de squad IDs a SquadIconData.
    /// </summary>
    /// <param name="squadIds">Lista de IDs de squads</param>
    /// <returns>Lista de SquadIconData válidos</returns>
    public static List<SquadIconData> ConvertToSquadIconDataList(List<string> squadIds)
    {
        if (squadIds == null) return new List<SquadIconData>();
        
        var result = new List<SquadIconData>();
        
        foreach (string squadId in squadIds)
        {
            var iconData = ConvertToSquadIconData(squadId);
            if (iconData != null)
                result.Add(iconData);
        }
        
        return result;
    }
    
    /// <summary>
    /// Convierte datos de héroe (loadout) a SquadIconData para UI.
    /// </summary>
    /// <param name="LoadOutSaveData">Datos del loadout</param>
    /// <returns>Lista de SquadIconData del loadout activo</returns>
    public static List<SquadIconData> ConvertLoadoutToSquadIconData(LoadoutSaveData loadoutData, HeroData heroData)
    {
        if (loadoutData?.squadInstanceIDs == null || loadoutData.squadInstanceIDs.Count == 0)
            return new List<SquadIconData>();

        List<string> heroSquadIDs = convertInstanceIDsToBaseSquadIDs(loadoutData.squadInstanceIDs, heroData?.squadProgress);
        if (heroSquadIDs.Count == 0)
            return new List<SquadIconData>();

        return ConvertToSquadIconDataList(heroSquadIDs);
    }
    /// <summary>
    /// Convierte una lista de squadInstanceIDs a sus correspondientes baseSquadIDs usando los datos del héroe.
    /// </summary>
    /// <param name="squadInstanceIDs">Lista de squadInstanceIDs</param>
    /// <param name="heroData">Datos del héroe para referencia</param>
    /// <returns>Lista de baseSquadIDs correspondientes</returns>
    public static List<string> convertInstanceIDsToBaseSquadIDs(List<string> squadInstanceIDs, List<SquadInstanceData> Instances)
    {
        if (squadInstanceIDs == null || squadInstanceIDs.Count == 0 || Instances == null)
            return new List<string>();

        var instanceIDSet = new HashSet<string>(squadInstanceIDs);
        return Instances
            .Where(squad => instanceIDSet.Contains(squad.id))
            .Select(squad => squad.baseSquadID)
            .ToList();
    }
    public static List<string> getBaseSquadIDsFromInstances(List<SquadInstanceData> Instances)
    {
        if (Instances == null || Instances.Count == 0)
            return new List<string>();

        return Instances.Select(squad => squad.baseSquadID).ToList();
    }
    
    #endregion

    #region Advanced API

    /// <summary>
    /// Obtiene squads por rango de costo de liderazgo.
    /// </summary>
    /// <param name="minCost">Costo mínimo</param>
    /// <param name="maxCost">Costo máximo</param>
    /// <returns>Lista de squads en el rango especificado</returns>
    public static List<SquadData> GetSquadsByLeadershipCost(int minCost, int maxCost)
    {
        EnsureInitialized();
        return _squadCache.Values.Where(squad => squad.leadershipCost >= minCost && squad.leadershipCost <= maxCost).ToList();
    }
    
    /// <summary>
    /// Busca squads por nombre (búsqueda parcial, case-insensitive).
    /// </summary>
    /// <param name="partialName">Nombre parcial a buscar</param>
    /// <returns>Lista de squads que coinciden</returns>
    public static List<SquadData> SearchSquadsByName(string partialName)
    {
        EnsureInitialized();
        
        if (string.IsNullOrEmpty(partialName))
            return new List<SquadData>();
        
        string searchTerm = partialName.ToLowerInvariant();
        return _squadCache.Values.Where(squad => 
            !string.IsNullOrEmpty(squad.squadName) && 
            squad.squadName.ToLowerInvariant().Contains(searchTerm)).ToList();
    }
    
    /// <summary>
    /// Calcula el costo total de liderazgo de una lista de squads.
    /// </summary>
    /// <param name="squadIds">Lista de IDs de squads</param>
    /// <returns>Costo total de liderazgo</returns>
    public static int GetTotalLeadershipCost(List<string> squadIds)
    {
        if (squadIds == null) return 0;
        
        int totalCost = 0;
        foreach (string squadId in squadIds)
        {
            var squadData = GetSquadById(squadId);
            if (squadData != null)
                totalCost += squadData.leadershipCost;
        }
        
        return totalCost;
    }
    #endregion
    
    #region Private Methods
    
    /// <summary>
    /// Asegura que el servicio esté inicializado antes de usar.
    /// </summary>
    private static void EnsureInitialized()
    {
        if (!_isInitialized)
            Initialize();
    }
    
    /// <summary>
    /// Carga la base de datos de squads desde Resources.
    /// </summary>
    private static void LoadSquadDatabase()
    {
        _squadDatabase = Resources.Load<SquadDatabase>("Data/Squads/SquadDatabase");
        
        if (_squadDatabase == null)
        {
            Debug.LogError("[SquadDataService] No se pudo cargar SquadDatabase desde Resources/Data/Squads/SquadDatabase");
            return;
        }
        
        if (_squadDatabase.allSquads == null)
        {
            Debug.LogError("[SquadDataService] SquadDatabase.allSquads es null");
            return;
        }
    }
    
    /// <summary>
    /// Construye los caches internos para optimizar búsquedas.
    /// </summary>
    private static void BuildCaches()
    {
        _squadCache = new Dictionary<string, SquadData>();
        _squadsByType = new Dictionary<UnitType, List<SquadData>>();
        
        if (_squadDatabase?.allSquads == null) return;
        
        // Construir cache por ID
        foreach (var squad in _squadDatabase.allSquads)
        {
            if (squad == null || string.IsNullOrEmpty(squad.id)) continue;
            
            if (_squadCache.ContainsKey(squad.id))
            {
                Debug.LogWarning($"[SquadDataService] Squad ID duplicado encontrado: {squad.id}");
                continue;
            }
            
            _squadCache[squad.id] = squad;
        }
        
        // Construir cache por tipo
        foreach (UnitType unitType in System.Enum.GetValues(typeof(UnitType)))
        {
            _squadsByType[unitType] = new List<SquadData>();
        }
        
        foreach (var squad in _squadCache.Values)
        {
            if (_squadsByType.ContainsKey(squad.unitType))
            {
                _squadsByType[squad.unitType].Add(squad);
            }
        }
    }
    
    #endregion
}
