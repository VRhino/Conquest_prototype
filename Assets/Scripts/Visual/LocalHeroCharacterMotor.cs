using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace ConquestTactics.Visual
{
    /// <summary>
    /// Sole physical executor for the local hero. ECS publishes
    /// <see cref="HeroMoveIntent"/>; this motor lets CharacterController resolve
    /// terrain and collisions, then publishes the confirmed pose and motor state.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class LocalHeroCharacterMotor : MonoBehaviour
    {
        private const float Gravity = -9.81f;
        private const float TerminalVelocity = -50f;
        private const float GroundCheckBuffer = -0.5f;

        private World _world;
        private Entity _heroEntity = Entity.Null;
        private CharacterController _controller;
        private float _verticalVelocity;
        private uint _appliedPositionRevision;
        private bool _syncPosition = true;
        private bool _syncRotation = true;
        private bool _isBound;

        public bool DebugLogging { get; set; }
        public bool IsBound => IsValidBinding();

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        public void Bind(World world, Entity heroEntity, bool syncPosition, bool syncRotation)
        {
            bool changedEntity = !_isBound || _world != world || _heroEntity != heroEntity;
            _world = world;
            _heroEntity = heroEntity;
            _syncPosition = syncPosition;
            _syncRotation = syncRotation;
            _isBound = world != null && world.IsCreated && heroEntity != Entity.Null;

            if (_controller == null)
                _controller = GetComponent<CharacterController>();

            if (!_isBound || _controller == null)
            {
                if (_controller != null) _controller.enabled = false;
                enabled = false;
                return;
            }

            enabled = true;
            _controller.enabled = true;
            if (!changedEntity)
                return;

            _verticalVelocity = 0f;
            _appliedPositionRevision = 0;
            EnsureMotorState();
            InitializeFromEntityPose();
        }

        public void ReleaseAuthority()
        {
            _isBound = false;
            _heroEntity = Entity.Null;
            _verticalVelocity = 0f;
            if (_controller != null) _controller.enabled = false;
            enabled = false;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void Tick(float deltaTime)
        {
            if (!IsValidBinding() || _controller == null || !_controller.enabled)
                return;

            var entityManager = _world.EntityManager;
            bool teleported = ApplySpawnPose(entityManager);
            bool canMove = !entityManager.HasComponent<HeroLifeComponent>(_heroEntity)
                || entityManager.GetComponentData<HeroLifeComponent>(_heroEntity).isAlive;
            if (entityManager.HasComponent<HeroSpawnComponent>(_heroEntity))
                canMove &= entityManager.GetComponentData<HeroSpawnComponent>(_heroEntity).hasSpawned;

            Vector3 before = transform.position;
            CollisionFlags collisionFlags = CollisionFlags.None;
            if (!teleported && canMove && entityManager.HasComponent<HeroMoveIntent>(_heroEntity))
            {
                var intent = entityManager.GetComponentData<HeroMoveIntent>(_heroEntity);
                Vector3 velocity = new Vector3(intent.Direction.x, 0f, intent.Direction.z) * intent.Speed;

                if (!_controller.isGrounded)
                    _verticalVelocity = Mathf.Max(
                        _verticalVelocity + Gravity * Mathf.Max(0f, deltaTime), TerminalVelocity);
                else
                    _verticalVelocity = GroundCheckBuffer;

                velocity.y = _verticalVelocity;
                collisionFlags = _controller.Move(velocity * Mathf.Max(0f, deltaTime));

                if (DebugLogging)
                    Debug.Log($"[LocalHeroCharacterMotor] Entity {_heroEntity.Index} intent={intent.Direction} " +
                              $"speed={intent.Speed:F2} position={transform.position}");
            }
            else
            {
                _verticalVelocity = 0f;
            }

            float3 confirmedVelocity = !teleported && canMove && deltaTime > 0f
                ? ((float3)transform.position - (float3)before) / deltaTime
                : float3.zero;
            PublishConfirmedState(entityManager, confirmedVelocity, collisionFlags);
        }

        private void InitializeFromEntityPose()
        {
            var entityManager = _world.EntityManager;
            if (!entityManager.Exists(_heroEntity)
                || !entityManager.HasComponent<LocalTransform>(_heroEntity))
                return;

            var pose = entityManager.GetComponentData<LocalTransform>(_heroEntity);
            SafeSetPose(pose.Position, pose.Rotation);
        }

        private bool ApplySpawnPose(EntityManager entityManager)
        {
            if (!entityManager.HasComponent<HeroSpawnComponent>(_heroEntity))
                return false;

            var spawn = entityManager.GetComponentData<HeroSpawnComponent>(_heroEntity);
            if (!spawn.hasSpawned || spawn.positionRevision == _appliedPositionRevision)
                return false;

            SafeSetPose(spawn.spawnPosition, spawn.spawnRotation);
            _verticalVelocity = 0f;
            _appliedPositionRevision = spawn.positionRevision;
            return true;
        }

        private void SafeSetPose(float3 position, quaternion rotation)
        {
            bool wasEnabled = _controller != null && _controller.enabled;
            if (wasEnabled) _controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            if (wasEnabled) _controller.enabled = true;
        }

        private void PublishConfirmedState(EntityManager entityManager, float3 velocity,
            CollisionFlags collisionFlags)
        {
            if (entityManager.HasComponent<LocalTransform>(_heroEntity))
            {
                var pose = entityManager.GetComponentData<LocalTransform>(_heroEntity);
                if (_syncPosition) pose.Position = transform.position;
                if (_syncRotation) pose.Rotation = transform.rotation;
                entityManager.SetComponentData(_heroEntity, pose);
            }

            EnsureMotorState();
            entityManager.SetComponentData(_heroEntity, new HeroMotorStateComponent
            {
                velocity = velocity,
                isGrounded = _controller.isGrounded,
                hitSides = (collisionFlags & CollisionFlags.Sides) != 0,
                hitCeiling = (collisionFlags & CollisionFlags.Above) != 0
            });
        }

        private void EnsureMotorState()
        {
            if (!IsValidBinding())
                return;
            var entityManager = _world.EntityManager;
            if (!entityManager.HasComponent<HeroMotorStateComponent>(_heroEntity))
                entityManager.AddComponentData(_heroEntity, default(HeroMotorStateComponent));
        }

        private bool IsValidBinding()
        {
            if (!_isBound || _world == null || !_world.IsCreated || _heroEntity == Entity.Null)
                return false;
            try
            {
                return _world.EntityManager.Exists(_heroEntity)
                    && _world.EntityManager.HasComponent<IsLocalPlayer>(_heroEntity);
            }
            catch (System.Exception)
            {
                return false;
            }
        }
    }
}
