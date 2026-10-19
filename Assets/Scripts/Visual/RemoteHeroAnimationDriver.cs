using System.Collections.Generic;
using ConquestTactics.Animation;
using Synty.AnimationBaseLocomotion.Samples;
using Unity.Entities;
using UnityEngine;
using UnityEngine.AI;

namespace ConquestTactics.Visual
{
    /// <summary>
    /// Drives remote-hero animation from confirmed NavMesh movement and ECS combat state.
    /// It never writes transforms or movement commands.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class RemoteHeroAnimationDriver : MonoBehaviour
    {
        [SerializeField] private LocomotionAnimationSettings _locomotion = new LocomotionAnimationSettings();
        private World _world;
        private Entity _heroEntity = Entity.Null;
        private NavMeshAgent _navAgent;
        private Animator _animator;
        private bool _wasMoving;
        private bool _isBound;
        private readonly Dictionary<int, float> _floatParameterCache = new();
        private readonly Dictionary<int, int> _integerParameterCache = new();
        private readonly Dictionary<int, bool> _boolParameterCache = new();
        private Animator _cachedParameterAnimator;
        private RuntimeAnimatorController _cachedParameterController;
        private bool _cachedAnimatorEnabled;
        private bool _hasParameterCacheState;

        public bool IsBound => IsValidBinding();

        private void Awake()
        {
            CacheComponents();
        }

        public void Bind(World world, Entity heroEntity)
        {
            bool changedEntity = !_isBound || _world != world || _heroEntity != heroEntity;
            _world = world;
            _heroEntity = heroEntity;
            _isBound = world != null && world.IsCreated && heroEntity != Entity.Null;
            CacheComponents();
            InvalidateAnimatorParameterCache();

            if (!_isBound)
            {
                enabled = false;
                return;
            }

            // Remote heroes must never read local input or rotate through the local controller.
            var localController = GetComponentInChildren<SamplePlayerAnimationController_ECS>(true);
            if (localController != null) localController.enabled = false;
            var inputAdapter = GetComponentInChildren<EcsAnimationInputAdapter>(true);
            if (inputAdapter != null) inputAdapter.enabled = false;

            if (changedEntity) _wasMoving = false;
            enabled = true;
        }

        public void Release()
        {
            _isBound = false;
            _heroEntity = Entity.Null;
            _wasMoving = false;
            InvalidateAnimatorParameterCache();
            enabled = false;
        }

        private void Update()
        {
            if (!IsValidBinding())
                return;

            CacheComponents();
            if (_animator == null)
                return;
            RefreshAnimatorParameterCache();

            if (_navAgent != null && _navAgent.enabled && _navAgent.isOnNavMesh)
                DriveLocomotion();
            else
                DriveStoppedLocomotion();

            DriveCombatAnimation();
        }

        private void DriveLocomotion()
        {
            Vector3 velocity = _navAgent.velocity;
            velocity.y = 0f;
            float speed = velocity.magnitude;
            var entityManager = _world.EntityManager;
            float baseSpeed = entityManager.HasComponent<HeroStatsComponent>(_heroEntity)
                ? entityManager.GetComponentData<HeroStatsComponent>(_heroEntity).baseSpeed
                : _navAgent.speed;
            bool sprinting = _navAgent.speed > baseSpeed * _locomotion.sprintSpeedRatio;
            var gait = _locomotion.GetGait(speed, baseSpeed, sprinting);
            bool isMoving = gait != LocomotionGait.Idle;
            Vector3 direction = isMoving ? transform.InverseTransformDirection(velocity.normalized) : Vector3.forward;

            bool justStartedMoving = isMoving && !_wasMoving;
            SetAnimatorFloat(AnimationHashes.MoveSpeed,
                _locomotion.GetBlendSpeed(speed, baseSpeed, _navAgent.speed, sprinting));
            SetAnimatorInteger(AnimationHashes.CurrentGait, (int)gait);
            SetAnimatorBool(AnimationHashes.IsGrounded, true);
            SetAnimatorBool(AnimationHashes.IsStopped, !isMoving);
            SetAnimatorBool(AnimationHashes.MovementInputHeld, isMoving && !justStartedMoving);
            SetAnimatorBool(AnimationHashes.MovementInputPressed, justStartedMoving);
            SetAnimatorBool(AnimationHashes.IsWalking, gait == LocomotionGait.Walk);
            SetAnimatorFloat(AnimationHashes.IsStrafing, isMoving ? 1f : 0f);
            SetAnimatorFloat(AnimationHashes.StrafeDirectionX, direction.x);
            SetAnimatorFloat(AnimationHashes.StrafeDirectionZ, direction.z);
            SetAnimatorFloat(AnimationHashes.ForwardStrafe, direction.z >= 0f ? 1f : 0f);
            SetAnimatorBool(AnimationHashes.MovementInputTapped, justStartedMoving);
            _wasMoving = isMoving;

            float headLookX = 0f;
            float headLookY = 0f;
            if (entityManager.HasComponent<HeroAnimationComponent>(_heroEntity))
            {
                var animation = entityManager.GetComponentData<HeroAnimationComponent>(_heroEntity);
                headLookX = animation.headLookX;
                headLookY = animation.headLookY;
            }
            SetAnimatorFloat(AnimationHashes.HeadLookX, headLookX);
            SetAnimatorFloat(AnimationHashes.HeadLookY, headLookY);
            SetAnimatorFloat(AnimationHashes.BodyLookX, 0f);
            SetAnimatorFloat(AnimationHashes.BodyLookY, 0f);
        }

        private void DriveStoppedLocomotion()
        {
            SetAnimatorFloat(AnimationHashes.MoveSpeed, 0f);
            SetAnimatorInteger(AnimationHashes.CurrentGait, 0);
            SetAnimatorBool(AnimationHashes.IsStopped, true);
            SetAnimatorBool(AnimationHashes.MovementInputHeld, false);
            SetAnimatorBool(AnimationHashes.MovementInputPressed, false);
            SetAnimatorBool(AnimationHashes.MovementInputTapped, false);
            SetAnimatorBool(AnimationHashes.IsWalking, false);
            SetAnimatorFloat(AnimationHashes.ForwardStrafe, 0f);
            SetAnimatorFloat(AnimationHashes.IsStrafing, 0f);
            SetAnimatorFloat(AnimationHashes.StrafeDirectionX, 0f);
            SetAnimatorFloat(AnimationHashes.StrafeDirectionZ, 1f);
            _wasMoving = false;
        }

        private void DriveCombatAnimation()
        {
            var entityManager = _world.EntityManager;
            if (entityManager.HasComponent<HeroAnimationComponent>(_heroEntity))
            {
                var animation = entityManager.GetComponentData<HeroAnimationComponent>(_heroEntity);
                if (animation.triggerAttack)
                {
                    _animator.SetTrigger(AnimationHashes.TriggerAttack);
                    animation.triggerAttack = false;
                    entityManager.SetComponentData(_heroEntity, animation);
                }
            }

            if (entityManager.HasComponent<HeroCombatComponent>(_heroEntity))
            {
                bool attacking = entityManager.GetComponentData<HeroCombatComponent>(_heroEntity).isAttacking;
                SetAnimatorBool(AnimationHashes.IsAttacking, attacking);
            }
        }

        private void CacheComponents()
        {
            if (_navAgent == null) _navAgent = GetComponent<NavMeshAgent>();
            if (_animator == null)
            {
                var animator = GetComponentInChildren<Animator>(true);
                if (animator != _animator)
                {
                    _animator = animator;
                    InvalidateAnimatorParameterCache();
                }
            }
        }

        private void OnEnable() => InvalidateAnimatorParameterCache();

        private void OnDisable() => InvalidateAnimatorParameterCache();

        private void RefreshAnimatorParameterCache()
        {
            var controller = _animator.runtimeAnimatorController;
            bool animatorEnabled = _animator.isActiveAndEnabled;
            if (_hasParameterCacheState && _cachedParameterAnimator == _animator
                && _cachedParameterController == controller
                && _cachedAnimatorEnabled == animatorEnabled)
                return;

            _floatParameterCache.Clear();
            _integerParameterCache.Clear();
            _boolParameterCache.Clear();
            _cachedParameterAnimator = _animator;
            _cachedParameterController = controller;
            _cachedAnimatorEnabled = animatorEnabled;
            _hasParameterCacheState = true;
        }

        private void InvalidateAnimatorParameterCache()
        {
            _floatParameterCache.Clear();
            _integerParameterCache.Clear();
            _boolParameterCache.Clear();
            _cachedParameterAnimator = null;
            _cachedParameterController = null;
            _hasParameterCacheState = false;
        }

        private void SetAnimatorFloat(int parameter, float value)
        {
            if (_floatParameterCache.TryGetValue(parameter, out float previous) && previous.Equals(value))
                return;
            _animator.SetFloat(parameter, value);
            _floatParameterCache[parameter] = value;
        }

        private void SetAnimatorInteger(int parameter, int value)
        {
            if (_integerParameterCache.TryGetValue(parameter, out int previous) && previous == value)
                return;
            _animator.SetInteger(parameter, value);
            _integerParameterCache[parameter] = value;
        }

        private void SetAnimatorBool(int parameter, bool value)
        {
            if (_boolParameterCache.TryGetValue(parameter, out bool previous) && previous == value)
                return;
            _animator.SetBool(parameter, value);
            _boolParameterCache[parameter] = value;
        }

        private bool IsValidBinding()
        {
            if (!_isBound || _world == null || !_world.IsCreated || _heroEntity == Entity.Null)
                return false;
            try
            {
                var entityManager = _world.EntityManager;
                return entityManager.Exists(_heroEntity)
                    && !entityManager.HasComponent<IsLocalPlayer>(_heroEntity)
                    && entityManager.HasComponent<HeroMoveIntent>(_heroEntity);
            }
            catch (System.Exception)
            {
                return false;
            }
        }
    }
}
