using System.Collections.Generic;
using UnityEngine;

namespace Data.Maps
{
    /// <summary>
    /// ScriptableObject que contiene los datos básicos de un mapa de batalla.
    /// </summary>
    [CreateAssetMenu(fileName = "New Map", menuName = "Maps/Map Data", order = 1)]
    public class MapDataSO : ScriptableObject
    {
        [Header("Map Identification")]
        [SerializeField] private string _mapId;
        [SerializeField] private string _mapName;

        [Header("Strategic Points")]
        [SerializeField] private List<string> _supplyPointIds = new List<string>();
        [SerializeField] private List<string> _capturePointIds = new List<string>();
        [SerializeField] private List<string> _spawnPointIds = new List<string>();

        [Header("Battle Configuration")]
        [SerializeField] private int _battleDurationSeconds = 1800; // En segundos (30 minutos por defecto)

        [Header("Scene Reference")]
        [SerializeField] private GameObject _preparationMap;

        #region Public Properties

        public string mapId => _mapId;
        public new string name => _mapName;
        public List<string> supplyPointIds => _supplyPointIds;
        public List<string> capturePointIds => _capturePointIds;
        public List<string> spawnPointIds => _spawnPointIds;
        public int battleDuration => _battleDurationSeconds;
        public GameObject preparationMap => _preparationMap;

        #endregion

        #region Validation

        /// <summary>
        /// Valida que el MapDataSO tenga configuración correcta.
        /// </summary>
        /// <returns>True si es válido, false si tiene errores</returns>
        public bool Validate()
        {
            bool isValid = true;

            if (string.IsNullOrEmpty(_mapId))
            {
                Debug.LogError($"[MapDataSO] {name}: Map ID cannot be empty", this);
                isValid = false;
            }

            if (string.IsNullOrEmpty(_mapName))
            {
                Debug.LogWarning($"[MapDataSO] {_mapId}: Map name is empty", this);
            }

            if (_battleDurationSeconds <= 0)
            {
                Debug.LogError($"[MapDataSO] {_mapId}: Battle duration must be positive", this);
                isValid = false;
            }

            if (_spawnPointIds.Count < 2)
            {
                Debug.LogWarning($"[MapDataSO] {_mapId}: Should have at least 2 spawn points for multiplayer", this);
            }

            return isValid;
        }

        #endregion

        #region Unity Events

        private void OnValidate()
        {
            // Auto-set name from file name if empty
            if (string.IsNullOrEmpty(_mapName))
            {
                _mapName = this.name;
            }

            // Auto-set ID from file name if empty
            if (string.IsNullOrEmpty(_mapId))
            {
                _mapId = this.name;
            }

            // Ensure minimum battle duration (1 minute)
            if (_battleDurationSeconds < 60)
            {
                _battleDurationSeconds = 60;
            }
        }

        #endregion
    }
}