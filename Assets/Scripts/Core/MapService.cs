using System.Collections.Generic;
using UnityEngine;
using Data.Maps;

/// <summary>
/// Servicio centralizado para acceso a datos de mapas.
/// Versión simple sin cache para acceso unificado a MapDatabase.
/// </summary>
public static class MapService
{
    #region Private Fields
    
    private static MapDatabase _mapDatabase;
    private static bool _isInitialized = false;
    
    #endregion
    
    #region Initialization
    
    /// <summary>
    /// Inicializa el servicio cargando la base de datos.
    /// </summary>
    public static void Initialize()
    {
        if (_isInitialized) return;
        
        LoadDatabase();
        if (_mapDatabase == null) return;
        
        _isInitialized = true;
        Debug.Log($"[MapService] Initialized successfully");
    }
    
    /// <summary>
    /// Verifica si el servicio está inicializado.
    /// </summary>
    public static bool IsInitialized => _isInitialized;
    
    /// <summary>
    /// Fuerza la reinicialización del servicio.
    /// </summary>
    public static void ForceReinitialize()
    {
        _isInitialized = false;
        _mapDatabase = null;
        Initialize();
    }
    
    #endregion
    
    #region Core API
    
    /// <summary>
    /// Obtiene un MapDataSO por su ID.
    /// </summary>
    /// <param name="mapId">ID único del mapa</param>
    /// <returns>MapDataSO encontrado o null si no existe</returns>
    public static MapDataSO GetMapById(string mapId)
    {
        EnsureInitialized();
        
        if (string.IsNullOrEmpty(mapId))
            return null;
        
        var map = _mapDatabase.GetMapById(mapId);
        if (map == null)
        {
            Debug.LogWarning($"[MapService] Map with ID '{mapId}' not found");
        }
        
        return map;
    }
    
    /// <summary>
    /// Obtiene todos los mapas disponibles.
    /// </summary>
    /// <returns>Lista completa de todos los mapas</returns>
    public static List<MapDataSO> GetAllMaps()
    {
        EnsureInitialized();
        return _mapDatabase.GetAllMaps();
    }
    
    /// <summary>
    /// Verifica si existe un mapa con el ID especificado.
    /// </summary>
    /// <param name="mapId">ID del mapa</param>
    /// <returns>True si existe</returns>
    public static bool MapExists(string mapId)
    {
        EnsureInitialized();
        
        if (string.IsNullOrEmpty(mapId)) return false;
        return _mapDatabase.MapExists(mapId);
    }
    
    #endregion

    #region Utility API

    /// <summary>
    /// Obtiene la duración de batalla de un mapa en minutos.
    /// </summary>
    /// <param name="mapId">ID del mapa</param>
    /// <returns>Duración en minutos, -1 si el mapa no existe</returns>
    public static float GetBattleDurationMinutes(string mapId)
    {
        var map = GetMapById(mapId);
        return map != null ? map.battleDuration / 60f : -1f;
    }
    
    /// <summary>
    /// Obtiene el total de puntos estratégicos de un mapa.
    /// </summary>
    /// <param name="mapId">ID del mapa</param>
    /// <returns>Número total de puntos (supply + capture), -1 si el mapa no existe</returns>
    public static int GetTotalStrategicPoints(string mapId)
    {
        var map = GetMapById(mapId);
        return map != null ? map.supplyPointIds.Count + map.capturePointIds.Count : -1;
    }
    
    /// <summary>
    /// Obtiene el número de spawn points de un mapa.
    /// </summary>
    /// <param name="mapId">ID del mapa</param>
    /// <returns>Número de spawn points, -1 si el mapa no existe</returns>
    public static int GetSpawnPointCount(string mapId)
    {
        var map = GetMapById(mapId);
        return map != null ? map.spawnPointIds.Count : -1;
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
    /// Carga la base de datos de mapas.
    /// </summary>
    private static void LoadDatabase()
    {
        _mapDatabase = MapDatabase.Instance;
        if (_mapDatabase == null)
        {
            Debug.LogError("[MapService] Failed to load MapDatabase!");
        }
    }
    
    #endregion
}