using Unity.Entities;
using Unity.Transforms;
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
        // Si la unidad está sprintando (normalized speed >= 1)
        public bool IsSprinting { get; private set; } = false;
        
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
        
        [Tooltip("Modo de depuración que fuerza el estado de movimiento para probar animaciones")]
        [SerializeField] private bool _forceMovementDebugMode = false;
        
        [Tooltip("Velocidad forzada para modo de depuración (0-1)")]
        [SerializeField] [Range(0, 1)] private float _forcedDebugSpeed = 0.5f;
        
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
            bool worldOk = _world != null;
            bool entityManagerOk = _entityManager != null;
            bool entityOk = _unitEntity != Entity.Null;
            bool existsOk = entityOk && _entityManager.Exists(_unitEntity);
            bool hasComponentOk = existsOk && _entityManager.HasComponent<UnitAnimationMovementComponent>(_unitEntity);

            if (_enableDebugLogs)
            {
                Debug.Log($"[UnitAnimationAdapter][IsValidSetup] worldOk: {worldOk}, entityManagerOk: {entityManagerOk}, entityOk: {entityOk}, existsOk: {existsOk}, hasComponentOk: {hasComponentOk}");
            }

            return worldOk && entityManagerOk && entityOk && existsOk && hasComponentOk;
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
                // En modo debug, usar valores forzados para pruebas
                if (_forceMovementDebugMode)
                {
                    NormalizedSpeed = _forcedDebugSpeed;
                    MovementVector = new Vector2(0, 1); // Dirección adelante
                    IsStopped = NormalizedSpeed < _stoppedThreshold;
                    IsRunning = NormalizedSpeed >= _runningThreshold;
                    IsSprinting = NormalizedSpeed >= 1f;
                    
                    // Log especial para modo debug
                    if (_enableDebugLogs && Time.frameCount % 60 == 0)
                    {
                        Debug.LogWarning($"[UnitAnimationAdapter] {gameObject.name} - MODO DEBUG FORZADO: Speed: {NormalizedSpeed:F2}, Stopped: {IsStopped}, Running: {IsRunning}, Sprinting: {IsSprinting}");
                    }
                    
                    return; // No procesamos datos de ECS en este modo
                }
                
                // Modo normal usando datos de ECS
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
                
                // Si el vector de movimiento es muy pequeño pero la velocidad no lo es,
                // asegurarnos de tener una dirección válida
                if (MovementVector.sqrMagnitude < 0.01f && NormalizedSpeed > _stoppedThreshold)
                {
                    MovementVector = new Vector2(0, 1); // Dirección adelante por defecto
                }
                
                // Comprobar si realmente hay movimiento físico (basado en Vector3.Distance)
                // Esto podría detectar cambios de posición incluso si el sistema ECS no reporta velocidad
                bool physicalMovement = false;
                if (_entityManager.HasComponent<LocalTransform>(_unitEntity))
                {
                    var ecsTransform = _entityManager.GetComponentData<LocalTransform>(_unitEntity);
                    float distance = Vector3.Distance(transform.position, new Vector3(ecsTransform.Position.x, ecsTransform.Position.y, ecsTransform.Position.z));
                    physicalMovement = distance > _stoppedThreshold * Time.deltaTime;
                }
                
                // Determinar estados, priorizando el movimiento físico observado
                IsStopped = !physicalMovement && NormalizedSpeed < _stoppedThreshold;
                IsRunning = NormalizedSpeed >= _runningThreshold && NormalizedSpeed < 0.8f;
                IsSprinting = NormalizedSpeed >= 0.8f;
                
                // Si la unidad no se ha movido desde el spawn, forzar valores a cero y detenido
                if (animData.CurrentSpeed < _stoppedThreshold && MovementVector.sqrMagnitude < 0.0001f)
                {
                    NormalizedSpeed = 0f;
                    MovementVector = Vector2.zero;
                    IsStopped = true;
                    IsRunning = false;
                    IsSprinting = false;
                    if (_enableDebugLogs)
                    {
                        Debug.Log($"[UnitAnimationAdapter.cs][{gameObject.name}] Entity: {_unitEntity} - Sin movimiento desde spawn: Speed=0, Vec=Vector2.zero, IsStopped=true");
                    }
                }
                
                // Modo de depuración para forzar movimiento
                if (_forceMovementDebugMode)
                {
                    NormalizedSpeed = _forcedDebugSpeed;
                    MovementVector = new Vector2(1, 0); // Forzar dirección
                    IsStopped = NormalizedSpeed < _stoppedThreshold;
                    IsRunning = NormalizedSpeed >= _runningThreshold;
                }
                
                if (_enableDebugLogs && Time.frameCount % 120 == 0) // Log cada ~2 segundos
                {
                    Debug.Log($"[UnitAnimationAdapter.cs][{gameObject.name}] Entity: {_unitEntity} - Speed: {NormalizedSpeed:F2}, Stopped: {IsStopped}, Running: {IsRunning}");
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
                IsSprinting = false;
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
        /// Muestra el estado actual del adaptador y la comunicación con EntityVisualSync y ECS (para debugging)
        /// </summary>
        [ContextMenu("Debug Adapter State (ECS ↔ Visual)")]
        public void DebugAdapterState()
        {
            var visualSync = GetComponent<ConquestTactics.Visual.EntityVisualSync>();
            Debug.Log($"=== [UnitAnimationAdapter] Debug State for {gameObject.name} ===");
            Debug.Log($"EntityVisualSync: {(visualSync != null ? "ENCONTRADO" : "NO ENCONTRADO")}");
            if (visualSync != null)
            {
                Debug.Log($"  - HeroEntity: {visualSync.GetHeroEntity()}");
            }
            Debug.Log($"UnitEntity (encontrada): {_unitEntity}");
            Debug.Log($"World: {_world?.Name ?? "null"}");
            Debug.Log($"EntityManager: {(_entityManager != null ? "OK" : "null")}");
            Debug.Log($"IsValidSetup: {IsValidSetup()}");
            if (_entityManager != null && _unitEntity != Entity.Null && _entityManager.HasComponent<UnitAnimationMovementComponent>(_unitEntity))
            {
                var animData = _entityManager.GetComponentData<UnitAnimationMovementComponent>(_unitEntity);
                Debug.Log($"[UnitAnimationAdapter.cs][{gameObject.name}] Entity: {_unitEntity} | [ECS] CurrentSpeed: {animData.CurrentSpeed:F3}, MaxSpeed: {animData.MaxSpeed:F3}, IsMoving: {animData.IsMoving}, IsRunning: {animData.IsRunning}");
                Debug.Log($"[UnitAnimationAdapter.cs][{gameObject.name}] Entity: {_unitEntity} | [ECS] MovementDirection: {animData.MovementDirection}");
            }
            else
            {
                Debug.LogWarning($"[UnitAnimationAdapter.cs][{gameObject.name}] Entity: {_unitEntity} | No se pudo leer UnitAnimationMovementComponent de la entidad asociada");
            }
            Debug.Log($"[UnitAnimationAdapter.cs][{gameObject.name}] Entity: {_unitEntity} | [ADAPTER] NormalizedSpeed: {NormalizedSpeed:F2}, IsStopped: {IsStopped}, IsRunning: {IsRunning}, MovementVector: {MovementVector}");
        }

        private void LateUpdate()
        {
            if (_enableDebugLogs && Time.frameCount % 60 == 0)
            {
                var visualSync = GetComponent<ConquestTactics.Visual.EntityVisualSync>();
                Debug.Log($"[UnitAnimationAdapter.cs][{gameObject.name}] Entity: {_unitEntity} | Valid: {IsValidSetup()} | VisualSync: {(visualSync != null ? "OK" : "NO")}");
                Debug.Log($"[UnitAnimationAdapter.cs][{gameObject.name}] Entity: {_unitEntity} | NormalizedSpeed: {NormalizedSpeed:F2} | IsStopped: {IsStopped} | IsRunning: {IsRunning}");
                if (_entityManager != null && _unitEntity != Entity.Null && _entityManager.HasComponent<UnitAnimationMovementComponent>(_unitEntity))
                {
                    var animData = _entityManager.GetComponentData<UnitAnimationMovementComponent>(_unitEntity);
                    Debug.Log($"[UnitAnimationAdapter.cs][{gameObject.name}] Entity: {_unitEntity} | [ECS] Speed: {animData.CurrentSpeed:F2} | Max: {animData.MaxSpeed:F2} | IsMoving: {animData.IsMoving} | IsRunning: {animData.IsRunning}");
                }
            }
        }
        #endregion
    }
}
