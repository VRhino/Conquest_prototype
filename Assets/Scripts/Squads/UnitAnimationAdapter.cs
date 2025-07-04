using Unity.Entities;
using UnityEngine;

namespace ConquestTactics.Animation
{
    /// <summary>
    /// Adaptador entre la lógica ECS y el sistema de animación de Unity para unidades.
    /// Similar a EcsAnimationInputAdapter pero no depende de input del jugador,
    /// solo se basa en el movimiento calculado por el ECS.
    /// </summary>
    public class UnitAnimationAdapter : MonoBehaviour
    {
        #region Public Properties
        
        // Velocidad normalizada (0-1) para el animador
        public float NormalizedSpeed { get; private set; }
        
        // Vector de movimiento (x,z) para las animaciones
        public Vector2 MovementVector { get; private set; }
        
        // Si la unidad está detenida
        public bool IsStopped { get; private set; } = true;
        
        // Si la unidad está corriendo
        public bool IsRunning { get; private set; } = false;
        
        #endregion
        
        #region Inspector Settings
        
        [Header("ECS Configuration")]
        [Tooltip("Si está marcado, busca automáticamente la entidad asociada")]
        [SerializeField] private bool _autoFindEntity = true;
        
        [Header("Animation Settings")]
        [Tooltip("Umbral de velocidad por encima del cual se considera que la unidad está corriendo")]
        [SerializeField] private float _runningThreshold = 0.6f;
        
        [Tooltip("Umbral de velocidad por debajo del cual se considera que la unidad está detenida")]
        [SerializeField] private float _stoppedThreshold = 0.05f;
        
        [Header("Debug")]
        [Tooltip("Mostrar información de debug en consola")]
        [SerializeField] private bool _enableDebugLogs = false;
        
        #endregion
        
        #region Private Fields
        
        private Entity _unitEntity;
        private EntityManager _entityManager;
        private World _world;
        
        // Cache para evitar allocaciones
        private EntityQuery _unitQuery;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Start()
        {
            InitializeEcsReferences();
            
            if (_autoFindEntity)
            {
                FindAssociatedEntity();
            }
            
            if (_enableDebugLogs)
            {
                Debug.Log($"[UnitAnimationAdapter] Initialized for {gameObject.name}");
            }
        }
        
        private void Update()
        {
            if (!IsValidSetup())
                return;
                
            UpdateFromEcs();
        }
        
        #endregion
        
        #region ECS Initialization
        
        private void InitializeEcsReferences()
        {
            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null)
            {
                Debug.LogError($"[UnitAnimationAdapter] No se encontró World ECS por defecto en {gameObject.name}");
                return;
            }
            
            _entityManager = _world.EntityManager;
            
            // Crear query para encontrar entidades de unidades
            _unitQuery = _entityManager.CreateEntityQuery(typeof(UnitAnimationMovementComponent));
        }
        
        private void FindAssociatedEntity()
        {
            // Intentar encontrar la entidad a través de EntityVisualSync si existe
            var visualSync = GetComponent<ConquestTactics.Visual.EntityVisualSync>();
            if (visualSync != null && visualSync.GetHeroEntity() != Entity.Null)
            {
                _unitEntity = visualSync.GetHeroEntity();
                
                if (_enableDebugLogs)
                {
                    Debug.Log($"[UnitAnimationAdapter] Entidad encontrada a través de EntityVisualSync: {_unitEntity}");
                }
                return;
            }
            
            // Alternativa: intentar encontrar por nombre/instancia
            // Este método es menos robusto pero sirve como fallback
            var allUnits = _unitQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (allUnits.Length > 0)
            {
                // Buscar una entidad que coincida con este GameObject
                // En una implementación real, deberías tener un mecanismo más robusto
                // para mapear GameObjects a entidades ECS
                
                _unitEntity = allUnits[0]; // Temporalmente usar la primera como ejemplo
                
                if (_enableDebugLogs)
                {
                    Debug.Log($"[UnitAnimationAdapter] Asignando entidad fallback: {_unitEntity}");
                }
            }
            else
            {
                if (_enableDebugLogs)
                {
                    Debug.LogWarning($"[UnitAnimationAdapter] No se encontraron entidades con UnitAnimationMovementComponent para {gameObject.name}");
                }
            }
            
            allUnits.Dispose();
        }
        
        private bool IsValidSetup()
        {
            return _world != null && 
                   _entityManager != null && 
                   _unitEntity != Entity.Null && 
                   _entityManager.Exists(_unitEntity) && 
                   _entityManager.HasComponent<UnitAnimationMovementComponent>(_unitEntity);
        }
        
        #endregion
        
        #region ECS Data Processing
        
        /// <summary>
        /// Actualiza los datos de animación desde el componente ECS
        /// </summary>
        private void UpdateFromEcs()
        {
            try
            {
                if (!_entityManager.HasComponent<UnitAnimationMovementComponent>(_unitEntity))
                {
                    if (_enableDebugLogs)
                    {
                        Debug.LogWarning($"[UnitAnimationAdapter] La entidad {_unitEntity} no tiene el componente UnitAnimationMovementComponent");
                    }
                    return;
                }
                
                var animData = _entityManager.GetComponentData<UnitAnimationMovementComponent>(_unitEntity);
                
                // Actualizar propiedades
                NormalizedSpeed = animData.MaxSpeed > 0 
                    ? Mathf.Clamp01(animData.CurrentSpeed / animData.MaxSpeed) 
                    : 0f;
                
                MovementVector = new Vector2(animData.MovementDirection.x, animData.MovementDirection.z);
                
                // Determinar estados
                IsStopped = NormalizedSpeed < _stoppedThreshold;
                IsRunning = NormalizedSpeed >= _runningThreshold;
                
                if (_enableDebugLogs && Time.frameCount % 120 == 0) // Log cada ~2 segundos
                {
                    Debug.Log($"[UnitAnimationAdapter] {gameObject.name} - Speed: {NormalizedSpeed:F2}, Stopped: {IsStopped}, Running: {IsRunning}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[UnitAnimationAdapter] Error obteniendo datos de ECS para {gameObject.name}: {e.Message}");
                
                // Valores por defecto en caso de error
                NormalizedSpeed = 0f;
                MovementVector = Vector2.zero;
                IsStopped = true;
                IsRunning = false;
            }
        }
        
        #endregion
        
        #region Public Interface
        
        /// <summary>
        /// Permite establecer manualmente la entidad asociada
        /// </summary>
        public void SetUnitEntity(Entity unitEntity)
        {
            _unitEntity = unitEntity;
            
            if (_enableDebugLogs)
            {
                Debug.Log($"[UnitAnimationAdapter] Entidad configurada manualmente para {gameObject.name}: {unitEntity}");
            }
        }
        
        #endregion
        
        #region Debug
        
        /// <summary>
        /// Muestra el estado actual del adaptador (para debugging)
        /// </summary>
        [ContextMenu("Debug Animation State")]
        public void DebugAnimationState()
        {
            Debug.Log($"=== Unit Animation Adapter State [{gameObject.name}] ===");
            Debug.Log($"Entity: {_unitEntity}");
            Debug.Log($"Normalized Speed: {NormalizedSpeed}");
            Debug.Log($"Movement Vector: {MovementVector}");
            Debug.Log($"Is Stopped: {IsStopped}");
            Debug.Log($"Is Running: {IsRunning}");
            Debug.Log($"Valid Setup: {IsValidSetup()}");
        }
        
        #endregion
    }
}
