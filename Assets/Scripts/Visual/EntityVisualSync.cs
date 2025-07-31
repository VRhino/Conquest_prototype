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
        [Header("Entity Configuration")]
        [SerializeField] private bool _autoFindHeroEntity = true;
        [SerializeField] private int _specificEntityId = -1;
        [Header("Synchronization Settings")]
        [SerializeField] private bool _syncPosition = true;
        [SerializeField] private bool _syncRotation = true;
        [SerializeField, HideInInspector] private bool _smoothMovement = false;
        [SerializeField] private float _smoothSpeed = 10f;
        [Header("Offset Settings")]
        [SerializeField] private Vector3 _positionOffset = Vector3.zero;
        [SerializeField] private Vector3 _rotationOffset = Vector3.zero;
        [Header("Debug")]
        [SerializeField] private bool _enableDebugLogs = true;
        [SerializeField] private bool _showDebugLines = false;
        
        private World _world;

        /// <summary>
        /// Indica si este visual representa al héroe local (con isLocalComponent en ECS).
        /// </summary>
        [HideInInspector]
        public bool IsLocalHero = false;
        private EntityManager _entityManager;
        private EntityQuery _heroQuery;
        private Entity _heroEntity = Entity.Null;
        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private bool _hasValidTarget;
        private bool _isManuallyConfigured = false;
        private float _lastSyncTime;
        private const float SYNC_INTERVAL = 0.016f;
        private CharacterController _characterController;
        private float _verticalVelocity = 0f;
        private const float GRAVITY = -9.81f;
        private const float TERMINAL_VELOCITY = -50f;
        private const float GROUND_CHECK_BUFFER = -0.5f;
        
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
                Debug.Log($"[EntityVisualSync] Start ejecutándose en {gameObject.name}");
            InitializeEcsReferences();
            if (_autoFindHeroEntity && !_isManuallyConfigured && _heroEntity == Entity.Null)
            {
                if (_enableDebugLogs)
                    Debug.Log($"[EntityVisualSync] Auto-buscando entidad héroe (no hay entidad configurada manualmente)");
                FindHeroEntity();
            }
            else if (_heroEntity != Entity.Null)
            {
                if (_enableDebugLogs)
                    Debug.Log($"[EntityVisualSync] Entidad ya configurada manualmente: {_heroEntity}, saltando auto-búsqueda");
            }


            // Si hay entidad válida, mover el visual a la posición del ECS antes de activar el CharacterController
            if (_heroEntity != Entity.Null && _entityManager != null && _entityManager.Exists(_heroEntity))
            {
                // Setear IsLocalHero si la entidad tiene el tag IsLocalPlayer
                IsLocalHero = _entityManager.HasComponent<IsLocalPlayer>(_heroEntity);
                if (_entityManager.HasComponent<LocalTransform>(_heroEntity))
                {
                    var ecsTransform = _entityManager.GetComponentData<LocalTransform>(_heroEntity);
                    transform.position = ecsTransform.Position;
                    transform.rotation = ecsTransform.Rotation;
                    if (_enableDebugLogs)
                        Debug.Log($"[EntityVisualSync] Posición visual inicializada desde ECS: {ecsTransform.Position}");
                }
                if (_enableDebugLogs)
                    Debug.Log($"[EntityVisualSync] IsLocalHero seteado a {IsLocalHero} para entidad {_heroEntity}");
            }

            DisableConflictingComponents();
            _targetPosition = transform.position;
            _targetRotation = transform.rotation;
            if (_enableDebugLogs)
                Debug.Log($"[EntityVisualSync] Initialized on {gameObject.name} at position {transform.position}");
        }

        private void Update()
        {
            // Eliminar throttle: correr Update cada frame
            if (!IsValidSetup())
            {
                if (_enableDebugLogs)
                    Debug.Log("[EntityVisualSync] Update skipped: IsValidSetup() == false");
                return;
            }
            if (_characterController != null && _characterController.enabled && IsValidSetup())
            {
                if (_entityManager.HasComponent<HeroMoveIntent>(_heroEntity))
                {
                    var moveIntent = _entityManager.GetComponentData<HeroMoveIntent>(_heroEntity);
                    Vector3 moveDir = new Vector3(moveIntent.Direction.x, 0f, moveIntent.Direction.z);
                    float speed = moveIntent.Speed;
                    Vector3 velocity = moveDir * speed;
                    if (_enableDebugLogs)
                    {
                        Debug.Log($"[EntityVisualSync] Frame {Time.frameCount} | moveIntent.Direction: {moveIntent.Direction} | moveIntent.Speed: {moveIntent.Speed}");
                        Debug.Log($"[EntityVisualSync] Frame {Time.frameCount} | PreMove Position: {transform.position}");
                        Debug.Log($"[EntityVisualSync] Frame {Time.frameCount} | velocity: {velocity}");
                    }
                    if (!_characterController.isGrounded)
                    {
                        _verticalVelocity += GRAVITY * Time.deltaTime;
                        if (_verticalVelocity < TERMINAL_VELOCITY)
                            _verticalVelocity = TERMINAL_VELOCITY;
                    }
                    else
                    {
                        _verticalVelocity = GROUND_CHECK_BUFFER;
                    }
                    velocity.y = _verticalVelocity;
                    _characterController.Move(velocity * Time.deltaTime);
                    if (_enableDebugLogs)
                    {
                        Debug.Log($"[EntityVisualSync] Frame {Time.frameCount} | PostMove Position: {transform.position}");
                    }
                }
            }
            if (_syncPosition || _syncRotation)
            {
                var ecsTransform = _entityManager.GetComponentData<LocalTransform>(_heroEntity);
                // Escritura de posición desactivada para depuración
                if (_syncRotation)
                {
                    ecsTransform.Rotation = transform.rotation;
                }
                _entityManager.SetComponentData(_heroEntity, ecsTransform);
            }
            _lastSyncTime = Time.time;
        }
        
        private void InitializeEcsReferences()
        {
            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null || !_world.IsCreated)
            {
                if (_enableDebugLogs)
                    Debug.LogWarning("[EntityVisualSync] World ECS no está disponible aún");
                return;
            }
            _entityManager = _world.EntityManager;
            if (_entityManager != null)
            {
                _heroQuery = _entityManager.CreateEntityQuery(
                    typeof(HeroInputComponent),
                    typeof(LocalTransform)
                );
                if (_enableDebugLogs)
                    Debug.Log("[EntityVisualSync] Referencias ECS inicializadas correctamente");
            }
        }

        private void FindHeroEntity()
        {
            if (_heroQuery.IsEmpty)
            {
                if (_enableDebugLogs)
                    Debug.LogWarning("[EntityVisualSync] No se encontró entidad con HeroInputComponent y LocalTransform");
                _hasValidTarget = false;
                return;
            }
            var heroEntities = _heroQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (heroEntities.Length > 0)
            {
                if (_specificEntityId >= 0 && _specificEntityId < heroEntities.Length)
                    _heroEntity = heroEntities[_specificEntityId];
                else
                    _heroEntity = heroEntities[0];
                _hasValidTarget = true;
                DisableConflictingComponents();
                if (_enableDebugLogs)
                    Debug.Log($"[EntityVisualSync] Entidad héroe encontrada: {_heroEntity}");
            }
            else
            {
                _hasValidTarget = false;
                if (_enableDebugLogs)
                    Debug.LogWarning("[EntityVisualSync] No se encontró entidad con HeroInputComponent y LocalTransform");
            }
            heroEntities.Dispose();
        }

        private bool IsValidSetup()
        {
            if (_world == null || !_world.IsCreated)
                return false;
            if (_entityManager == null)
                return false;
            if (_heroEntity == Entity.Null)
                return false;
            try
            {
                return _entityManager.Exists(_heroEntity) && _hasValidTarget;
            }
            catch (System.Exception)
            {
                return false;
            }
        }
        
        //
        
        public void SetHeroEntity(Entity heroEntity)
        {
            if (_entityManager == null || _world == null || !_world.IsCreated)
            {
                InitializeEcsReferences();
                if (_entityManager == null)
                {
                    if (_enableDebugLogs)
                        Debug.LogWarning("[EntityVisualSync] No se pudo inicializar EntityManager en SetHeroEntity");
                    return;
                }
            }
            DisableConflictingComponents();
            if (_enableDebugLogs)
                Debug.Log($"[EntityVisualSync] Entidad héroe configurada manualmente: {heroEntity}, válida: {_hasValidTarget}");
        }

        public void RefreshHeroEntity()
        {
            _isManuallyConfigured = false;
            FindHeroEntity();
        }

        public Entity GetHeroEntity()
        {
            return _heroEntity;
        }
        
        private void OnDrawGizmos()
        {
            if (!_showDebugLines || !_hasValidTarget) return;
            if (_entityManager != null && _entityManager.Exists(_heroEntity))
            {
                var ecsTransform = _entityManager.GetComponentData<LocalTransform>(_heroEntity);
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, ecsTransform.Position);
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(ecsTransform.Position, 0.5f);
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(transform.position, 0.3f);
            }
        }
        
        private void DisableConflictingComponents()
        {
            CharacterController characterController = GetComponent<CharacterController>();
            if (characterController != null && characterController.enabled)
            {
                if (_enableDebugLogs)
                    Debug.LogWarning($"[EntityVisualSync] Disabling CharacterController on {gameObject.name} to prevent position conflicts");
                characterController.enabled = true;
            }
        }
    }
}