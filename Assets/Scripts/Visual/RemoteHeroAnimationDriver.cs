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
        private World _world;
        private Entity _heroEntity = Entity.Null;
        private NavMeshAgent _navAgent;
        private Animator _animator;
        private bool _wasMoving;
        private bool _isBound;

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
            enabled = false;
        }

        private void Update()
        {
            if (!IsValidBinding())
                return;

            CacheComponents();
            if (_animator == null)
                return;

            if (_navAgent != null && _navAgent.enabled && _navAgent.isOnNavMesh)
                DriveLocomotion();
            else
                DriveStoppedLocomotion();

            DriveCombatAnimation();
        }

        private void DriveLocomotion()
        {
            float speed = _navAgent.velocity.magnitude;
            float maxSpeed = _navAgent.speed > 0f ? _navAgent.speed : 1f;
            var entityManager = _world.EntityManager;
            bool sprinting = entityManager.HasComponent<HeroAIDecision>(_heroEntity)
                && entityManager.GetComponentData<HeroAIDecision>(_heroEntity).shouldSprint
                && speed > 0.1f;
            bool isMoving = speed > 0.1f;

            int gait = 0;
            if (isMoving)
            {
                if (sprinting) gait = 3;
                else if (speed / maxSpeed > 0.6f) gait = 2;
                else gait = 1;
            }

            bool justStartedMoving = isMoving && !_wasMoving;
            _animator.SetFloat(AnimationHashes.MoveSpeed, isMoving ? speed : 0f);
            _animator.SetInteger(AnimationHashes.CurrentGait, gait);
            _animator.SetBool(AnimationHashes.IsGrounded, true);
            _animator.SetBool(AnimationHashes.IsStopped, !isMoving);
            _animator.SetBool(AnimationHashes.MovementInputHeld, isMoving);
            _animator.SetBool(AnimationHashes.MovementInputPressed, isMoving);
            _animator.SetBool(AnimationHashes.IsWalking, gait == 1);
            _animator.SetFloat(AnimationHashes.ForwardStrafe, isMoving ? 1f : 0f);
            _animator.SetBool(AnimationHashes.MovementInputTapped, justStartedMoving);
            _wasMoving = isMoving;

            float headLookX = 0f;
            float headLookY = 0f;
            if (entityManager.HasComponent<HeroAnimationComponent>(_heroEntity))
            {
                var animation = entityManager.GetComponentData<HeroAnimationComponent>(_heroEntity);
                headLookX = animation.headLookX;
                headLookY = animation.headLookY;
            }
            _animator.SetFloat(AnimationHashes.HeadLookX, headLookX);
            _animator.SetFloat(AnimationHashes.HeadLookY, headLookY);
            _animator.SetFloat(AnimationHashes.BodyLookX, 0f);
            _animator.SetFloat(AnimationHashes.BodyLookY, 0f);
        }

        private void DriveStoppedLocomotion()
        {
            _animator.SetFloat(AnimationHashes.MoveSpeed, 0f);
            _animator.SetInteger(AnimationHashes.CurrentGait, 0);
            _animator.SetBool(AnimationHashes.IsStopped, true);
            _animator.SetBool(AnimationHashes.MovementInputHeld, false);
            _animator.SetBool(AnimationHashes.MovementInputPressed, false);
            _animator.SetBool(AnimationHashes.MovementInputTapped, false);
            _animator.SetBool(AnimationHashes.IsWalking, false);
            _animator.SetFloat(AnimationHashes.ForwardStrafe, 0f);
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
                _animator.SetBool(AnimationHashes.IsAttacking, attacking);
            }
        }

        private void CacheComponents()
        {
            if (_navAgent == null) _navAgent = GetComponent<NavMeshAgent>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>(true);
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
