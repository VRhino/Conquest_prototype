using System.Collections.Generic;
using UnityEngine;

namespace Data.Items
{
    /// <summary>
    /// Modular ItemDatabase using ItemDataSO for scalable item management.
    /// Each item is its own ScriptableObject asset for better organization.
    /// </summary>
    [CreateAssetMenu(menuName = "Items/Item Database")]
    public class EnhancedItemDatabase : ScriptableObject
    {
        [Header("Modular Items")]
        [SerializeField] private List<ItemDataSO> _items = new List<ItemDataSO>();

        // Cache para performance
        private Dictionary<string, ItemDataSO> _itemsCache;
        private bool _cacheBuilt = false;

        #region Singleton Pattern

        private static EnhancedItemDatabase _instance;
        public static EnhancedItemDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<EnhancedItemDatabase>("Items/ItemSODatabase");
                    if (_instance == null) 
                    {
                        Debug.LogError("[EnhancedItemDatabase] No EnhancedItemDatabase found in Resources/Items/ folder!");
                        return null;
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
        /// Obtiene un ItemDataSO por ID.
        /// </summary>
        /// <param name="id">ID del ítem</param>
        /// <returns>ItemDataSO o null si no se encuentra</returns>
        public ItemDataSO GetItemDataSOById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            EnsureCacheBuilt();
            _itemsCache.TryGetValue(id, out ItemDataSO itemDataSO);
            return itemDataSO;
        }

        /// <summary>
        /// Obtiene todos los ítems.
        /// </summary>
        /// <returns>Lista de todos los ítems</returns>
        public List<ItemDataSO> GetAllItems()
        {
            EnsureCacheBuilt();
            return _items != null ? new List<ItemDataSO>(_items) : new List<ItemDataSO>();
        }

        /// <summary>
        /// Obtiene todos los ítems como una colección de solo lectura (más eficiente).
        /// </summary>
        /// <returns>Colección de solo lectura de todos los ítems</returns>
        public IReadOnlyCollection<ItemDataSO> GetAllItemsReadOnly()
        {
            EnsureCacheBuilt();
            return _itemsCache.Values;
        }

        /// <summary>
        /// Verifica si existe un ítem con el ID especificado.
        /// </summary>
        /// <param name="id">ID del ítem</param>
        /// <returns>True si existe</returns>
        public bool ItemExists(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;

            EnsureCacheBuilt();
            return _itemsCache.ContainsKey(id);
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
                itemCount = _itemsCache.Count,
                totalItemCount = _itemsCache.Count
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
            if (_itemsCache == null)
                _itemsCache = new Dictionary<string, ItemDataSO>();
            else
                _itemsCache.Clear();

            // Validar y cache modular items
            if (_items != null)
            {
                var duplicateIds = new HashSet<string>();
                
                foreach (var item in _items)
                {
                    if (item == null)
                    {
                        Debug.LogWarning("[EnhancedItemDatabase] Null item reference found in items list");
                        continue;
                    }
                    
                    if (string.IsNullOrEmpty(item.id))
                    {
                        Debug.LogWarning($"[EnhancedItemDatabase] Item with empty ID found: {item.name}");
                        continue;
                    }

                    if (_itemsCache.ContainsKey(item.id))
                    {
                        if (!duplicateIds.Contains(item.id))
                        {
                            Debug.LogError($"[EnhancedItemDatabase] CRITICAL: Duplicate item ID '{item.id}' found! This will cause unpredictable behavior.");
                            duplicateIds.Add(item.id);
                        }
                        continue;
                    }
                    
                    _itemsCache[item.id] = item;
                }
            }

            _cacheBuilt = true;
            Debug.Log($"[EnhancedItemDatabase] Cache built successfully: {_itemsCache.Count} items");
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
        /// Limpia completamente el caché (útil para liberar memoria).
        /// </summary>
        public void ClearCache()
        {
            _itemsCache?.Clear();
            _cacheBuilt = false;
        }

        /// <summary>
        /// Verifica la integridad del caché y lo reconstruye si es necesario.
        /// </summary>
        public void ValidateAndRepairCache()
        {
            if (!_cacheBuilt || _itemsCache == null)
            {
                BuildCache();
                return;
            }

            // Verificar consistencia
            bool needsRebuild = false;
            
            if (_items != null)
            {
                foreach (var item in _items)
                {
                    if (item != null && !string.IsNullOrEmpty(item.id))
                    {
                        if (!_itemsCache.ContainsKey(item.id))
                        {
                            Debug.LogWarning($"[EnhancedItemDatabase] Cache missing item: {item.id}. Rebuilding cache.");
                            needsRebuild = true;
                            break;
                        }
                    }
                }
            }

            if (needsRebuild)
            {
                RebuildCache();
            }
        }

        #endregion

        #region Management

        /// <summary>
        /// Agrega un ItemDataSO a la lista de ítems.
        /// Nota: Esta operación requiere guardar manualmente el asset para persistir cambios.
        /// </summary>
        /// <param name="itemDataSO">ItemDataSO a agregar</param>
        /// <returns>True si se agregó exitosamente, false si ya existe o es inválido</returns>
        public bool AddItem(ItemDataSO itemDataSO)
        {
            if (itemDataSO == null) 
            {
                Debug.LogWarning("[EnhancedItemDatabase] Cannot add null ItemDataSO");
                return false;
            }

            if (string.IsNullOrEmpty(itemDataSO.id))
            {
                Debug.LogWarning($"[EnhancedItemDatabase] Cannot add ItemDataSO with empty ID: {itemDataSO.name}");
                return false;
            }

            if (_items == null)
                _items = new List<ItemDataSO>();

            // Verificar duplicados
            if (ItemExists(itemDataSO.id))
            {
                Debug.LogWarning($"[EnhancedItemDatabase] Item with ID '{itemDataSO.id}' already exists");
                return false;
            }

            _items.Add(itemDataSO);
            
            // Agregar directamente a caché en lugar de reconstruir todo
            if (_cacheBuilt && _itemsCache != null)
            {
                _itemsCache[itemDataSO.id] = itemDataSO;
            }

            Debug.Log($"[EnhancedItemDatabase] Item '{itemDataSO.id}' added successfully");
            return true;
        }

        /// <summary>
        /// Remueve un ItemDataSO de la lista de ítems.
        /// </summary>
        /// <param name="itemId">ID del ítem a remover</param>
        /// <returns>True si se removió exitosamente</returns>
        public bool RemoveItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;

            if (_items != null)
            {
                var item = _items.Find(i => i != null && i.id == itemId);
                if (item != null)
                {
                    _items.Remove(item);
                    
                    // Remover de caché también
                    if (_cacheBuilt && _itemsCache != null)
                    {
                        _itemsCache.Remove(itemId);
                    }
                    
                    return true;
                }
            }
            
            return false;
        }

        #endregion

        #region Validation

        /// <summary>
        /// Valida todos los ítems en el database.
        /// </summary>
        [ContextMenu("Validate Database")]
        public void ValidateDatabase()
        {
            int errors = 0;
            int warnings = 0;
            var seenIds = new HashSet<string>();

            // Validar items
            if (_items != null)
            {
                Debug.Log("[EnhancedItemDatabase] Validating items...");
                
                foreach (var item in _items)
                {
                    if (item == null)
                    {
                        Debug.LogError("[EnhancedItemDatabase] Null item reference found");
                        errors++;
                        continue;
                    }

                    // Validar IDs duplicados
                    if (!string.IsNullOrEmpty(item.id))
                    {
                        if (seenIds.Contains(item.id))
                        {
                            Debug.LogError($"[EnhancedItemDatabase] DUPLICATE ID FOUND: '{item.id}' - This WILL cause runtime errors!");
                            errors++;
                        }
                        else
                        {
                            seenIds.Add(item.id);
                        }
                    }

                    if (!item.Validate()) warnings++;
                }
            }

            // Validar consistencia de caché
            if (_cacheBuilt && _itemsCache != null)
            {
                if (_itemsCache.Count != seenIds.Count)
                {
                    Debug.LogWarning($"[EnhancedItemDatabase] Cache inconsistency detected: cache has {_itemsCache.Count} items, but items list has {seenIds.Count} valid items");
                    warnings++;
                }
            }

            var stats = GetDatabaseStats();
            if (errors == 0) Debug.Log($"[EnhancedItemDatabase] Validation complete: {stats.totalItemCount} items, {warnings} warnings, no errors");
            else Debug.LogError($"[EnhancedItemDatabase] Validation FAILED: {errors} errors and {warnings} warnings found!");
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
            // Solo en el editor y si tenemos items
            #if UNITY_EDITOR
            if (_items != null && _items.Count > 0)
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
            public int itemCount;
            public int totalItemCount;
        }

        #endregion
    }
}