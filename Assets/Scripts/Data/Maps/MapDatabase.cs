using System.Collections.Generic;
using UnityEngine;

namespace Data.Maps
{
    /// <summary>
    /// Database simple para mapas disponibles en el juego.
    /// Maneja la colección de todos los MapDataSO del proyecto.
    /// </summary>
    [CreateAssetMenu(menuName = "Maps/Map Database")]
    public class MapDatabase : ScriptableObject
    {
        [Header("Available Maps")]
        [SerializeField] private List<MapDataSO> _maps = new List<MapDataSO>();

        // Cache para performance
        private Dictionary<string, MapDataSO> _mapCache;
        private bool _cacheBuilt = false;

        #region Singleton Pattern

        private static MapDatabase _instance;
        public static MapDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<MapDatabase>("Maps/MapDatabase");
                    if (_instance == null)
                    {
                        Debug.LogError("[MapDatabase] No MapDatabase found in Resources/Maps/ folder!");
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Reset singleton instance (útil para testing y play mode changes)
        /// </summary>
        public static void ResetInstance()
        {
            _instance = null;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Obtiene un MapDataSO por ID.
        /// </summary>
        /// <param name="mapId">ID del mapa</param>
        /// <returns>MapDataSO o null si no se encuentra</returns>
        public MapDataSO GetMapById(string mapId)
        {
            if (string.IsNullOrEmpty(mapId)) return null;

            EnsureCacheBuilt();
            _mapCache.TryGetValue(mapId, out MapDataSO map);
            return map;
        }

        /// <summary>
        /// Obtiene todos los mapas disponibles.
        /// </summary>
        /// <returns>Lista de todos los mapas</returns>
        public List<MapDataSO> GetAllMaps()
        {
            return _maps != null ? new List<MapDataSO>(_maps) : new List<MapDataSO>();
        }

        /// <summary>
        /// Verifica si existe un mapa con el ID especificado.
        /// </summary>
        /// <param name="mapId">ID del mapa</param>
        /// <returns>True si existe</returns>
        public bool MapExists(string mapId)
        {
            if (string.IsNullOrEmpty(mapId)) return false;

            EnsureCacheBuilt();
            return _mapCache.ContainsKey(mapId);
        }

        /// <summary>
        /// Obtiene estadísticas del database.
        /// </summary>
        /// <returns>Información sobre el estado del database</returns>
        public DatabaseStats GetDatabaseStats()
        {
            EnsureCacheBuilt();
            return new DatabaseStats
            {
                mapCount = _mapCache.Count,
                totalMapCount = _mapCache.Count
            };
        }

        #endregion

        #region Cache Management

        /// <summary>
        /// Asegura que el cache esté construido.
        /// </summary>
        private void EnsureCacheBuilt()
        {
            if (!_cacheBuilt) BuildCache();
        }

        /// <summary>
        /// Construye el cache interno para optimizar búsquedas.
        /// </summary>
        private void BuildCache()
        {
            if (_mapCache == null)
                _mapCache = new Dictionary<string, MapDataSO>();
            else
                _mapCache.Clear();

            // Cache maps
            if (_maps != null)
            {
                var duplicateIds = new HashSet<string>();
                
                foreach (var map in _maps)
                {
                    if (map == null)
                    {
                        Debug.LogWarning("[MapDatabase] Null map reference found in maps list");
                        continue;
                    }
                    
                    if (string.IsNullOrEmpty(map.mapId))
                    {
                        Debug.LogWarning($"[MapDatabase] Map with empty ID found: {map.name}");
                        continue;
                    }

                    if (_mapCache.ContainsKey(map.mapId))
                    {
                        if (!duplicateIds.Contains(map.mapId))
                        {
                            Debug.LogError($"[MapDatabase] CRITICAL: Duplicate map ID '{map.mapId}' found! This will cause unpredictable behavior.");
                            duplicateIds.Add(map.mapId);
                        }
                        continue;
                    }
                    
                    _mapCache[map.mapId] = map;
                }
            }

            _cacheBuilt = true;
            Debug.Log($"[MapDatabase] Cache built successfully: {_mapCache.Count} maps");
        }

        /// <summary>
        /// Fuerza la reconstrucción del cache.
        /// </summary>
        public void RebuildCache()
        {
            _cacheBuilt = false;
            BuildCache();
        }

        /// <summary>
        /// Limpia completamente el caché.
        /// </summary>
        public void ClearCache()
        {
            _mapCache?.Clear();
            _cacheBuilt = false;
        }

        #endregion

        #region Management

        /// <summary>
        /// Agrega un MapDataSO a la lista de mapas.
        /// </summary>
        /// <param name="mapDataSO">MapDataSO a agregar</param>
        /// <returns>True si se agregó exitosamente</returns>
        public bool AddMap(MapDataSO mapDataSO)
        {
            if (mapDataSO == null) 
            {
                Debug.LogWarning("[MapDatabase] Cannot add null MapDataSO");
                return false;
            }

            if (string.IsNullOrEmpty(mapDataSO.mapId))
            {
                Debug.LogWarning($"[MapDatabase] Cannot add MapDataSO with empty ID: {mapDataSO.name}");
                return false;
            }

            if (_maps == null)
                _maps = new List<MapDataSO>();

            // Verificar duplicados
            if (MapExists(mapDataSO.mapId))
            {
                Debug.LogWarning($"[MapDatabase] Map with ID '{mapDataSO.mapId}' already exists");
                return false;
            }

            _maps.Add(mapDataSO);
            
            // Agregar directamente a caché en lugar de reconstruir todo
            if (_cacheBuilt && _mapCache != null)
            {
                _mapCache[mapDataSO.mapId] = mapDataSO;
            }

            Debug.Log($"[MapDatabase] Map '{mapDataSO.mapId}' added successfully");
            return true;
        }

        /// <summary>
        /// Remueve un MapDataSO de la lista de mapas.
        /// </summary>
        /// <param name="mapId">ID del mapa a remover</param>
        /// <returns>True si se removió exitosamente</returns>
        public bool RemoveMap(string mapId)
        {
            if (string.IsNullOrEmpty(mapId)) return false;

            if (_maps != null)
            {
                var map = _maps.Find(m => m != null && m.mapId == mapId);
                if (map != null)
                {
                    _maps.Remove(map);
                    
                    // Remover de caché también
                    if (_cacheBuilt && _mapCache != null)
                    {
                        _mapCache.Remove(mapId);
                    }
                    
                    return true;
                }
            }
            
            return false;
        }

        #endregion

        #region Validation

        /// <summary>
        /// Valida todos los mapas en el database.
        /// </summary>
        [ContextMenu("Validate Database")]
        public void ValidateDatabase()
        {
            int errors = 0;
            int warnings = 0;
            var seenIds = new HashSet<string>();

            // Validar maps
            if (_maps != null)
            {
                Debug.Log("[MapDatabase] Validating maps...");
                
                foreach (var map in _maps)
                {
                    if (map == null)
                    {
                        Debug.LogError("[MapDatabase] Null map reference found");
                        errors++;
                        continue;
                    }

                    // Validar IDs duplicados
                    if (!string.IsNullOrEmpty(map.mapId))
                    {
                        if (seenIds.Contains(map.mapId))
                        {
                            Debug.LogError($"[MapDatabase] DUPLICATE ID FOUND: '{map.mapId}' - This WILL cause runtime errors!");
                            errors++;
                        }
                        else
                        {
                            seenIds.Add(map.mapId);
                        }
                    }

                    if (!map.Validate()) warnings++;
                }
            }

            // Validar consistencia de caché
            if (_cacheBuilt && _mapCache != null)
            {
                if (_mapCache.Count != seenIds.Count)
                {
                    Debug.LogWarning($"[MapDatabase] Cache inconsistency detected: cache has {_mapCache.Count} maps, but maps list has {seenIds.Count} valid maps");
                    warnings++;
                }
            }

            var stats = GetDatabaseStats();
            if (errors == 0) Debug.Log($"[MapDatabase] Validation complete: {stats.totalMapCount} maps, {warnings} warnings, no errors");
            else Debug.LogError($"[MapDatabase] Validation FAILED: {errors} errors and {warnings} warnings found!");
        }

        #endregion

        #region Unity Events

        /// <summary>
        /// Llamado cuando el objeto es destruido. Limpia referencias singleton.
        /// </summary>
        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// Validación automática cuando se modifica el asset en el editor.
        /// </summary>
        private void OnValidate()
        {
            // Solo en el editor y si tenemos mapas
            #if UNITY_EDITOR
            if (_maps != null && _maps.Count > 0)
            {
                // Forzar reconstrucción de caché en la próxima consulta
                _cacheBuilt = false;
            }
            #endif
        }

        #endregion

        #region Data Structures

        [System.Serializable]
        public struct DatabaseStats
        {
            public int mapCount;
            public int totalMapCount;
        }

        #endregion
    }
}