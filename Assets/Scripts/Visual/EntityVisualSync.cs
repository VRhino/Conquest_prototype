using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;
using ConquestTactics.Animation;
using Synty.AnimationBaseLocomotion.Samples;

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
        private NavMeshAgent _navAgent;
        private Animator _animator;

        // Animation parameter hashes are centralized in AnimationHashes.cs
        private bool _positionInitialized = false;
        private bool _remoteWasMoving = false;
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
            _navAgent     = GetComponent<NavMeshAgent>();
            _animator     = GetComponentInChildren<Animator>(true);
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

                // Remote heroes: disable local-player animation components so they don't
                // overwrite the Animator values that EntityVisualSync drives directly.
                if (!IsLocalHero)
                {
                    var animController = GetComponentInChildren<SamplePlayerAnimationController_ECS>(true);
                    if (animController != null) animController.enabled = false;
                    var inputAdapter = GetComponentInChildren<EcsAnimationInputAdapter>(true);
                    if (inputAdapter != null) inputAdapter.enabled = false;
                }

                if (_entityManager.HasComponent<LocalTransform>(_heroEntity))
                {
                    var ecsTransform = _entityManager.GetComponentData<LocalTransform>(_heroEntity);

                    // Safe teleport: disable CC before setting position to avoid internal state desync.
                    // Only re-enable for local hero — remote heroes use NavMeshAgent, not CC.
                    if (_characterController != null) _characterController.enabled = false;
                    transform.position = ecsTransform.Position;
                    transform.rotation = ecsTransform.Rotation;
                    if (_characterController != null) _characterController.enabled = IsLocalHero;

                    _positionInitialized = true;
                    if (_enableDebugLogs)
                        Debug.Log($"[EntityVisualSync] Posición visual inicializada desde ECS: {ecsTransform.Position}");
                }
                if (_enableDebugLogs)
                    Debug.Log($"[EntityVisualSync] IsLocalHero seteado a {IsLocalHero} para entidad {_heroEntity}");

                // [BattleTestDebug] Remote hero setup summary
                if (!IsLocalHero)
                {
                    var navAgentCheck = GetComponent<NavMeshAgent>();
                    var animCheck     = GetComponentInChildren<Animator>(true);
                    var ctrlCheck     = GetComponentInChildren<SamplePlayerAnimationController_ECS>(true);
                    string animCtrlName = (animCheck != null && animCheck.runtimeAnimatorController != null)
                        ? animCheck.runtimeAnimatorController.name : "NULL";
                    Debug.Log($"[BattleTestDebug][RemoteSetup] {gameObject.name} | NavMeshAgent={navAgentCheck != null} navEnabled={navAgentCheck?.enabled} navOnMesh={navAgentCheck?.isOnNavMesh} | Animator={animCheck != null} animCtrl={animCtrlName} | SampleCtrl={ctrlCheck != null} ctrlEnabled={ctrlCheck?.enabled}");
                }
            }

            DisableConflictingComponents();
            _targetPosition = transform.position;
            _targetRotation = transform.rotation;
            if (_enableDebugLogs)
                Debug.Log($"[EntityVisualSync] Initialized on {gameObject.name} at position {transform.position}");
        }

        private void Update()
        {
            // If the linked ECS entity was destroyed, destroy this visual GameObject
            if (_hasValidTarget && _heroEntity != Entity.Null && _entityManager != null
                && (!_entityManager.Exists(_heroEntity)))
            {
                if (_enableDebugLogs)
                    Debug.Log($"[EntityVisualSync] Entity {_heroEntity} destroyed — destroying visual {gameObject.name}");
                Destroy(gameObject);
                return;
            }

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
                bool isHero = _entityManager.HasComponent<HeroMoveIntent>(_heroEntity);
                if (isHero && _positionInitialized && IsLocalHero)
                {
                    // Local hero: GO is authoritative — write GO position and rotation back to ECS
                    ecsTransform.Position = new float3(transform.position.x, transform.position.y, transform.position.z);
                    if (_syncRotation)
                    {
                        ecsTransform.Rotation = transform.rotation;
                    }
                    _entityManager.SetComponentData(_heroEntity, ecsTransform);
                }
                else
                {
                    // Lazy-init: NavMeshAgent is added dynamically after Awake by HeroVisualManagementSystem
                    if (_navAgent == null)
                        _navAgent = GetComponent<NavMeshAgent>();

                    bool navNull    = _navAgent == null;
                    bool navEnabled = !navNull && _navAgent.enabled;
                    bool navOnMesh  = !navNull && _navAgent.isOnNavMesh;
                    bool usesNavMesh = navEnabled && navOnMesh;

                    // [BattleTestDebug] Log why usesNavMesh is false (once per 60 frames)
                    if (!usesNavMesh && Time.frameCount % 60 == 0)
                        Debug.Log($"[BattleTestDebug][RemoteAnim] {gameObject.name} usesNavMesh=FALSE | navNull={navNull} navEnabled={navEnabled} navOnMesh={navOnMesh} IsLocalHero={IsLocalHero}");

                    if (usesNavMesh)
                    {
                        // [Sprint5] Position sync delegated to NavMeshPositionSyncSystem when syncPositionFromNavMesh == true
                        bool ecsHandlesPosition = _entityManager.HasComponent<NavAgentComponent>(_heroEntity)
                            && _entityManager.GetComponentData<NavAgentComponent>(_heroEntity).syncPositionFromNavMesh;
                        if (_syncPosition && !ecsHandlesPosition)
                            ecsTransform.Position = new float3(transform.position.x, transform.position.y, transform.position.z);
                        if (_syncRotation)
                            ecsTransform.Rotation = transform.rotation;
                        _entityManager.SetComponentData(_heroEntity, ecsTransform);

                        // Drive locomotion animation directly from NavMeshAgent velocity.
                        // No dependency on EcsAnimationInputAdapter or SamplePlayerAnimationController —
                        // the remote prefab only needs an Animator with MoveSpeed/CurrentGait/IsGrounded params.
                        if (_animator != null)
                        {
                            float speed    = _navAgent.velocity.magnitude;
                            float maxSpeed = _navAgent.speed > 0f ? _navAgent.speed : 1f;
                            bool sprinting = _entityManager.HasComponent<HeroAIDecision>(_heroEntity)
                                && _entityManager.GetComponentData<HeroAIDecision>(_heroEntity).shouldSprint
                                && speed > 0.1f;

                            // GaitState: 0=Idle, 1=Walk, 2=Run, 3=Sprint
                            int gait = 0;
                            if (speed > 0.1f)
                            {
                                if (sprinting)              gait = 3;
                                else if (speed / maxSpeed > 0.6f) gait = 2;
                                else                        gait = 1;
                            }

                            bool isMoving = speed > 0.1f;

                            // [BattleTestDebug] Log animation state every 60 frames
                            if (Time.frameCount % 60 == 0)
                                Debug.Log($"[BattleTestDebug][RemoteAnim] {gameObject.name} speed={speed:F2} gait={gait} isMoving={isMoving} fwdStrafe={( isMoving ? 1f : 0f )} animState={_animator.GetCurrentAnimatorStateInfo(0).fullPathHash} animCtrl={(_animator.runtimeAnimatorController != null ? _animator.runtimeAnimatorController.name : "NULL")}");

                            bool justStartedMoving = isMoving && !_remoteWasMoving;
                            bool justStoppedMoving = !isMoving && _remoteWasMoving;

                            _animator.SetFloat(AnimationHashes.MoveSpeed, isMoving ? speed : 0f);
                            _animator.SetInteger(AnimationHashes.CurrentGait, gait);
                            _animator.SetBool(AnimationHashes.IsGrounded, true);
                            _animator.SetBool(AnimationHashes.IsStopped, !isMoving);
                            _animator.SetBool(AnimationHashes.MovementInputHeld, isMoving);
                            _animator.SetBool(AnimationHashes.MovementInputPressed, isMoving);
                            _animator.SetBool(AnimationHashes.IsWalking, gait == 1);
                            _animator.SetFloat(AnimationHashes.ForwardStrafe, isMoving ? 1f : 0f);
                            // MovementInputTapped must be a ONE-FRAME pulse — keeping it true causes
                            // the controller to loop back to the start state every frame.
                            _animator.SetBool(AnimationHashes.MovementInputTapped, justStartedMoving);

                            if (justStartedMoving)
                                Debug.Log($"[BattleTestDebug][RemoteAnim] {gameObject.name} movement STARTED");
                            else if (justStoppedMoving)
                                Debug.Log($"[BattleTestDebug][RemoteAnim] {gameObject.name} movement STOPPED");

                            _remoteWasMoving = isMoving;

                            // Drive upper body params to prevent T-pose (BUG-002)
                            // For remote AI heroes: headLookX/Y are replicated from the server via HeroAnimationComponent.
                            // For local-context AI heroes: values default to 0 (neutral head pose).
                            float remoteHeadLookX = 0f;
                            float remoteHeadLookY = 0f;
                            if (_entityManager.HasComponent<HeroAnimationComponent>(_heroEntity))
                            {
                                var remoteAnim = _entityManager.GetComponentData<HeroAnimationComponent>(_heroEntity);
                                remoteHeadLookX = remoteAnim.headLookX;
                                remoteHeadLookY = remoteAnim.headLookY;
                            }
                            _animator.SetFloat(AnimationHashes.HeadLookX, remoteHeadLookX);
                            _animator.SetFloat(AnimationHashes.HeadLookY, remoteHeadLookY);
                            _animator.SetFloat(AnimationHashes.BodyLookX, 0f);
                            _animator.SetFloat(AnimationHashes.BodyLookY, 0f);
                        }
                        else
                        {
                            // [BattleTestDebug] Animator missing
                            if (Time.frameCount % 60 == 0)
                                Debug.LogWarning($"[BattleTestDebug][RemoteAnim] {gameObject.name} _animator is NULL — cannot drive animations");
                        }
                    }
                    else
                    {
                        // Units without NavMesh: ECS is authoritative — sync ECS → GO
                        if (_syncPosition)
                            transform.position = ecsTransform.Position;
                        if (_syncRotation)
                            transform.rotation = ecsTransform.Rotation;
                    }
                }
            }
            // Sync attack animation state from ECS → Animator
            if (_animator != null && _entityManager.HasComponent<HeroAnimationComponent>(_heroEntity))
            {
                var animData = _entityManager.GetComponentData<HeroAnimationComponent>(_heroEntity);
                if (animData.triggerAttack)
                {
                    _animator.SetTrigger(AnimationHashes.TriggerAttack);
                    animData.triggerAttack = false;
                    _entityManager.SetComponentData(_heroEntity, animData);
                }

                // Drive IsAttacking bool so the Animator Controller can exit the attack state
                if (_entityManager.HasComponent<HeroCombatComponent>(_heroEntity))
                {
                    bool attacking = _entityManager.GetComponentData<HeroCombatComponent>(_heroEntity).isAttacking;
                    _animator.SetBool(AnimationHashes.IsAttacking, attacking);
                }
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
                    typeof(LocalTransform),
                    typeof(IsLocalPlayer)
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
            _heroEntity = heroEntity;
            _isManuallyConfigured = true;
            _hasValidTarget = true;
            DisableConflictingComponents();
            if (_enableDebugLogs)
                Debug.Log($"[EntityVisualSync] Entidad configurada manualmente: {heroEntity}");
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