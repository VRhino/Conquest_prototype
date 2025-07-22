using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;

namespace ConquestTactics.Visual
{
    /// <summary>
    /// Sincroniza la posición y rotación de un GameObject con una entidad ECS.
    /// Permite mantener la representación visual en sync con la lógica ECS.
    /// </summary>
    public class EntityVisualSync : MonoBehaviour
    {
        #region Inspector Settings
        
        [Header("Entity Configuration")]
        [Tooltip("Si está marcado, busca automáticamente la entidad héroe")]
        [SerializeField] private bool _autoFindHeroEntity = true;
        
        [Tooltip("ID específico de la entidad (opcional, para múltiples héroes)")]
        [SerializeField] private int _specificEntityId = -1;
        
        [Header("Synchronization Settings")]
        [Tooltip("Sincronizar posición con la entidad ECS")]
        [SerializeField] private bool _syncPosition = true;
        
        [Tooltip("Sincronizar rotación con la entidad ECS")]
        [SerializeField] private bool _syncRotation = true;
        
        [Tooltip("Suavizar el movimiento visual")]
        [SerializeField, HideInInspector] private bool _smoothMovement = false; // Forzar a false por código
        
        [Tooltip("Velocidad de suavizado del movimiento")]
        [SerializeField] private float _smoothSpeed = 10f;
        
        [Header("Offset Settings")]
        [Tooltip("Offset de posición aplicado al visual")]
        [SerializeField] private Vector3 _positionOffset = Vector3.zero;
        
        [Tooltip("Offset de rotación aplicado al visual")]
        [SerializeField] private Vector3 _rotationOffset = Vector3.zero;
        
        [Header("Debug")]
        [Tooltip("Mostrar información de debug")]
        [SerializeField] private bool _enableDebugLogs = true;
        
        [Tooltip("Mostrar líneas de debug en Scene view")]
        [SerializeField] private bool _showDebugLines = false;
        
        #endregion
        
        #region Private Fields
        
        // ECS References
        private World _world;
        private EntityManager _entityManager;
        private EntityQuery _heroQuery;
        private Entity _heroEntity = Entity.Null;
        
        // Transform synchronization
        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private bool _hasValidTarget;
        private bool _isManuallyConfigured = false; // Nueva bandera para evitar conflictos
        
        // Performance optimization
        private float _lastSyncTime;
        private const float SYNC_INTERVAL = 0.016f; // ~60 FPS

        private CharacterController _characterController;

        // Gravity/vertical velocity handling
        private float _verticalVelocity = 0f;
        private const float GRAVITY = -9.81f;
        private const float TERMINAL_VELOCITY = -50f;
        private const float GROUND_CHECK_BUFFER = -0.5f; // Small negative to keep grounded
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            if (_characterController != null && !_characterController.enabled)
            {
                _characterController.enabled = true;
                if (_enableDebugLogs)
                    Debug.Log("[EntityVisualSync] CharacterController habilitado para movimiento visual");
            }
        }

        private void Start()
        {
            if (_enableDebugLogs)
            {
                Debug.Log($"[EntityVisualSync] Start ejecutándose en {gameObject.name}");
            }
            InitializeEcsReferences();
            
            // Disable any components that might conflict with our position control
            DisableConflictingComponents();
            
            // Solo auto-buscar si no se ha configurado una entidad manualmente
            if (_autoFindHeroEntity && !_isManuallyConfigured && _heroEntity == Entity.Null)
            {
                if (_enableDebugLogs)
                {
                    Debug.Log($"[EntityVisualSync] Auto-buscando entidad héroe (no hay entidad configurada manualmente)");
                }
                FindHeroEntity();
            }
            else if (_heroEntity != Entity.Null)
            {
                if (_enableDebugLogs)
                {
                    Debug.Log($"[EntityVisualSync] Entidad ya configurada manualmente: {_heroEntity}, saltando auto-búsqueda");
                }
            }
            
            // Inicializar valores target
            _targetPosition = transform.position;
            _targetRotation = transform.rotation;
            
            if (_enableDebugLogs)
            {
                Debug.Log($"[EntityVisualSync] Initialized on {gameObject.name} at position {transform.position}");
            }
        }
        
        private void Update()
        {
            // Optimización: no sincronizar cada frame si no es necesario
            if (Time.time - _lastSyncTime < SYNC_INTERVAL)
                return;
                 
            // 1. Leer intención de movimiento ECS y mover el CharacterController
            if (IsValidSetup() && _characterController != null && _characterController.enabled)
            {
                Vector3 move = Vector3.zero;
                if (_entityManager.HasComponent<HeroMoveIntent>(_heroEntity))
                {
                    var moveIntent = _entityManager.GetComponentData<HeroMoveIntent>(_heroEntity);
                    move = new Vector3(moveIntent.Direction.x, 0, moveIntent.Direction.z) * moveIntent.Speed;
                }
                // Gravity/vertical velocity logic
                if (_characterController.isGrounded)
                {
                    if (_verticalVelocity < 0)
                        _verticalVelocity = GROUND_CHECK_BUFFER; // Small negative to keep grounded
                }
                else
                {
                    _verticalVelocity += GRAVITY * Time.deltaTime;
                    if (_verticalVelocity < TERMINAL_VELOCITY)
                        _verticalVelocity = TERMINAL_VELOCITY;
                }
                move.y = _verticalVelocity;
                _characterController.Move(move * Time.deltaTime);
            }
            // 2. Actualizar ECS LocalTransform con la posición visual después del movimiento
            if (IsValidSetup())
            {
                var ecsTransform = _entityManager.GetComponentData<LocalTransform>(_heroEntity);
                float3 newPos = transform.position - _positionOffset;
                ecsTransform.Position = newPos;
                _entityManager.SetComponentData(_heroEntity, ecsTransform);
            }
            // 3. Sincronizar rotación si corresponde
            if (_syncRotation && IsValidSetup())
            {
                var ecsTransform = _entityManager.GetComponentData<LocalTransform>(_heroEntity);
                ecsTransform.Rotation = transform.rotation;
                _entityManager.SetComponentData(_heroEntity, ecsTransform);
            }
            
            _lastSyncTime = Time.time;
        }
        
        private void OnDestroy()
        {
            // EntityQuery is a struct and doesn't need explicit disposal
        }
        
        #endregion
        
        #region ECS Initialization
        
        private void InitializeEcsReferences()
        {
            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null || !_world.IsCreated)
            {
                if (_enableDebugLogs)
                {
                    Debug.LogWarning("[EntityVisualSync] World ECS no está disponible aún");
                }
                return;
            }
            
            _entityManager = _world.EntityManager;
            
            // Solo crear el query si el EntityManager está disponible
            if (_entityManager != null)
            {
                // Crear query para encontrar entidades héroe
                _heroQuery = _entityManager.CreateEntityQuery(
                    typeof(HeroInputComponent),
                    typeof(LocalTransform)
                );
                
                if (_enableDebugLogs)
                {
                    Debug.Log("[EntityVisualSync] Referencias ECS inicializadas correctamente");
                }
            }
        }
        
        private void FindHeroEntity()
        {
            if (_heroQuery.IsEmpty)
            {
                if (_enableDebugLogs)
                {
                    Debug.LogWarning("[EntityVisualSync] No se encontró entidad con HeroInputComponent y LocalTransform");
                }
                _hasValidTarget = false;
                return;
            }
            
            var heroEntities = _heroQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            
            if (heroEntities.Length > 0)
            {
                // Si se especifica un ID específico, buscar esa entidad
                if (_specificEntityId >= 0 && _specificEntityId < heroEntities.Length)
                {
                    _heroEntity = heroEntities[_specificEntityId];
                }
                else
                {
                    // Usar la primera entidad encontrada
                    _heroEntity = heroEntities[0];
                }
                
                _hasValidTarget = true;
                
                // Make sure no components interfere with our position control
                DisableConflictingComponents();
                
                if (_enableDebugLogs)
                {
                    Debug.Log($"[EntityVisualSync] Entidad héroe encontrada: {_heroEntity}");
                }
            }
            else
            {
                _hasValidTarget = false;
                
                if (_enableDebugLogs)
                {
                    Debug.LogWarning("[EntityVisualSync] No se encontró entidad con HeroInputComponent y LocalTransform");
                }
            }
            
            heroEntities.Dispose();
        }
        
        private bool IsValidSetup()
        {
            // Verificar que el world ECS está disponible
            if (_world == null || !_world.IsCreated)
            {
                return false;
            }
            
            // Verificar que el EntityManager está disponible
            if (_entityManager == null)
            {
                return false;
            }
            
            // Verificar que tenemos una entidad válida
            if (_heroEntity == Entity.Null)
            {
                return false;
            }
            
            // Verificar que la entidad aún existe
            try
            {
                return _entityManager.Exists(_heroEntity) && _hasValidTarget;
            }
            catch (System.Exception)
            {
                // El EntityManager puede no estar disponible durante transiciones
                return false;
            }
        }
        
        #endregion
        
        #region Transform Synchronization
        
        // Position caching for interference detection
        private Vector3 _lastSetPosition = Vector3.zero;
        private Vector3 _positionBeforeSync = Vector3.zero;
        private float _lastDetectionTime = 0f;
        
        private void SyncTransformFromEcs()
        {
            if (!IsValidSetup())
            {
                if (_enableDebugLogs && Time.frameCount % 120 == 0)
                {
                    Debug.LogWarning($"[EntityVisualSync] Setup no válido - World: {_world?.IsCreated}, EntityManager: {_entityManager != null}, Entity: {_heroEntity}, HasTarget: {_hasValidTarget}");
                }
                
                // Intentar encontrar la entidad nuevamente si se perdió y no está configurada manualmente
                if (_autoFindHeroEntity && !_isManuallyConfigured && Time.frameCount % 60 == 0) // Cada segundo aproximadamente
                {
                    FindHeroEntity();
                }
                return;
            }

            try
            {
                // Check for position interference from other scripts
                DetectPositionInterference();
                
                // Store current position before sync
                _positionBeforeSync = transform.position;
                
                var ecsTransform = _entityManager.GetComponentData<LocalTransform>(_heroEntity);
                
                // Aplicar offsets
                Vector3 targetPos = (Vector3)ecsTransform.Position + _positionOffset;
                Quaternion targetRot = math.mul(ecsTransform.Rotation, quaternion.Euler(math.radians(_rotationOffset)));
                
                // Logs más frecuentes para debug inicial
                bool isInitialSync = _isManuallyConfigured && Time.time < 5f; // Primeros 5 segundos
                if (_syncPosition)
                {
                    _targetPosition = targetPos;
                    // Forzar siempre movimiento instantáneo
                    transform.position = _targetPosition;
                    _lastSetPosition = transform.position;
                }
                
                if (_syncRotation)
                {
                    _targetRotation = targetRot;
                    // Forzar siempre rotación instantánea
                    transform.rotation = _targetRotation;
                }
            }
            catch (System.Exception e)
            {
                if (_enableDebugLogs)
                {
                    Debug.LogError($"[EntityVisualSync] Error durante sincronización: {e.Message}");
                }
                _hasValidTarget = false;
            }
        }
        
        #endregion
        
        #region Public Interface
        
        /// <summary>
        /// Permite configurar manualmente una entidad específica
        /// </summary>
        /// <param name="heroEntity">La entidad héroe a sincronizar</param>
        public void SetHeroEntity(Entity heroEntity)
        {
            // Asegurar que tenemos referencias ECS válidas antes de proceder
            if (_entityManager == null || _world == null || !_world.IsCreated)
            {
                InitializeEcsReferences();
                // Si aún no podemos inicializar, diferir la configuración
                if (_entityManager == null)
                {
                    if (_enableDebugLogs)
                    {
                        Debug.LogWarning("[EntityVisualSync] No se puede configurar héroe - EntityManager no disponible aún");
                    }
                    return;
                }
            }
            _heroEntity = heroEntity;
            _hasValidTarget = _entityManager.Exists(heroEntity);
            _isManuallyConfigured = true; // Marcar como configurado manualmente
            // Make sure no components interfere with our position control
            DisableConflictingComponents();
            if (_enableDebugLogs)
            {
                Debug.Log($"[EntityVisualSync] Entidad héroe configurada manualmente: {heroEntity}, válida: {_hasValidTarget}");
            }
            // Forzar sincronización inmediata para evitar desfase inicial
            if (_hasValidTarget)
            {
                ForceSyncToEcsPosition();
            }
            else
            {
                if (_enableDebugLogs)
                {
                    Debug.LogWarning($"[EntityVisualSync] No se pudo validar la entidad {heroEntity}");
                }
            }
        }

        /// <summary>
        /// Fuerza la posición y rotación del visual a la del ECS, desactivando momentáneamente el CharacterController si es necesario
        /// </summary>
        private void ForceSyncToEcsPosition()
        {
            if (!IsValidSetup()) return;
            var ecsTransform = _entityManager.GetComponentData<LocalTransform>(_heroEntity);
            bool wasEnabled = false;
            if (_characterController != null)
            {
                wasEnabled = _characterController.enabled;
                _characterController.enabled = false;
            }
            if (_syncPosition)
            {
                transform.position = (Vector3)ecsTransform.Position + _positionOffset;
            }
            if (_syncRotation)
            {
                transform.rotation = math.mul(ecsTransform.Rotation, quaternion.Euler(math.radians(_rotationOffset)));
            }
            if (_characterController != null && wasEnabled)
            {
                _characterController.enabled = true;
            }
            if (_enableDebugLogs)
            {
                Debug.Log($"[EntityVisualSync] ForceSyncToEcsPosition ejecutado. Visual pos: {transform.position}, ECS pos: {ecsTransform.Position}");
            }
        }
        
        /// <summary>
        /// Fuerza la búsqueda de una nueva entidad héroe
        /// </summary>
        public void RefreshHeroEntity()
        {
            _isManuallyConfigured = false; // Permitir auto-búsqueda nuevamente
            FindHeroEntity();
        }
        
        /// <summary>
        /// Teleporta inmediatamente el visual a la posición de la entidad ECS
        /// </summary>
        public void TeleportToEntity()
        {
            if (!IsValidSetup()) return;
            
            var ecsTransform = _entityManager.GetComponentData<LocalTransform>(_heroEntity);
            
            if (_syncPosition)
            {
                transform.position = (Vector3)ecsTransform.Position + _positionOffset;
            }
            
            if (_syncRotation)
            {
                transform.rotation = math.mul(ecsTransform.Rotation, quaternion.Euler(math.radians(_rotationOffset)));
            }
        }
        
        /// <summary>
        /// Obtiene la entidad ECS asociada
        /// </summary>
        public Entity GetHeroEntity()
        {
            return _heroEntity;
        }
        
        #endregion
        
        #region Debug Visualization
        
        private void OnDrawGizmos()
        {
            if (!_showDebugLines || !_hasValidTarget) return;
            
            // Mostrar línea entre el visual y la entidad ECS
            if (_entityManager != null && _entityManager.Exists(_heroEntity))
            {
                var ecsTransform = _entityManager.GetComponentData<LocalTransform>(_heroEntity);
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, ecsTransform.Position);
                
                // Mostrar la posición de la entidad ECS
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(ecsTransform.Position, 0.5f);
                
                // Mostrar la posición del visual
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(transform.position, 0.3f);
            }
        }
        
        #endregion
        
        #region Interference Detection

        /// <summary>
        /// Detects if other components are interfering with position control
        /// </summary>
        private void DetectPositionInterference()
        {
            // Skip first frame check
            if (_lastSetPosition == Vector3.zero)
                return;
            
            // Only check every 0.5 seconds to reduce spam
            if (Time.time - _lastDetectionTime < 0.5f) 
                return;
            
            _lastDetectionTime = Time.time;
            
            // Check if position has been changed by another component
            if (_lastSetPosition != transform.position)
            {
                Vector3 diff = transform.position - _lastSetPosition;
                if (diff.sqrMagnitude > 0.001f)
                {
                    if (_enableDebugLogs)
                    {
                        Debug.LogWarning($"[EntityVisualSync] POSITION INTERFERENCE DETECTED on {gameObject.name}!");
                        Debug.LogWarning($"[EntityVisualSync] Expected: {_lastSetPosition}, Actual: {transform.position}, Difference: {diff}");
                        Debug.LogWarning("[EntityVisualSync] Components that might be responsible:");
                    }
                    // Check for common position-modifying components
                    if (_enableDebugLogs)
                    {
                        if (TryGetComponent<CharacterController>(out var cc))
                            Debug.LogWarning($"[EntityVisualSync] - CharacterController (enabled: {cc.enabled})");
                        if (TryGetComponent<Rigidbody>(out var rb))
                            Debug.LogWarning($"[EntityVisualSync] - Rigidbody (isKinematic: {rb.isKinematic})");
                        if (TryGetComponent<Animator>(out var anim))
                            Debug.LogWarning($"[EntityVisualSync] - Animator (applyRootMotion: {anim.applyRootMotion})");
                        var behaviors = GetComponents<MonoBehaviour>();
                        foreach (var behavior in behaviors)
                        {
                            if (behavior != this && behavior.enabled)
                                Debug.LogWarning($"[EntityVisualSync] - {behavior.GetType().Name} (enabled)");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Disable components that may interfere with position control
        /// </summary>
        private void DisableConflictingComponents()
        {
            CharacterController characterController = GetComponent<CharacterController>();
            if (characterController != null && characterController.enabled)
            {
                if (_enableDebugLogs)
                {
                    Debug.LogWarning($"[EntityVisualSync] Disabling CharacterController on {gameObject.name} to prevent position conflicts");
                }
                characterController.enabled = true;
            }
            // Rigidbody rb = GetComponent<Rigidbody>();
            // if (rb != null && !rb.isKinematic)
            // {
            //     if (_enableDebugLogs)
            //     {
            //         Debug.LogWarning($"[EntityVisualSync] Setting Rigidbody to kinematic on {gameObject.name} to prevent position conflicts");
            //     }
            //     rb.isKinematic = true;
            // }
            // Animator animator = GetComponent<Animator>();
            // if (animator != null && animator.applyRootMotion)
            // {
            //     if (_enableDebugLogs)
            //     {
            //         Debug.LogWarning($"[EntityVisualSync] Disabling root motion on Animator in {gameObject.name} to prevent position conflicts");
            //     }
            //     animator.applyRootMotion = false;
            // }
        }

        #endregion
    }
}