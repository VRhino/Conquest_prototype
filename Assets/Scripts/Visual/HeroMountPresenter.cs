using System.Collections.Generic;
using ConquestTactics.Animation;
using Synty.AnimationBaseLocomotion.Samples;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;

namespace ConquestTactics.Visual
{
    /// <summary>
    /// The hero on horseback (M19), visual side. It reads <see cref="HeroMountComponent"/> and never writes ECS.
    /// - The hero's root stays its body (motor or agent, camera point, hit collider). The horse
    ///   (<see cref="HeroHorseVisual"/>) is a child of the root, at its centre.
    /// - The hero's Animator gets the rider controller of its weapon. Its clips (Hero_Mount, the gaits, the attack,
    ///   Hero_Dismount) are in the seat's space, like the soldiers' riders. After the animation, the pelvis is carried
    ///   from the root to the saddle (LateUpdate), so the rider follows the saddle at every step.
    /// - While riding, the on-foot animation drivers are paused. The shield is put away (the left hand holds the
    ///   reins), and the body's collider covers horse and rider: <see cref="HeroMountRules.HitTarget(EntityManager, Entity, float3, float)"/>
    ///   splits the blows by height. The camera point goes up to the rider's head.
    /// - The rider turns at the horse's rate: the root faces the heading that HeroMovementSystem worked out.
    /// - Getting off, or being thrown, puts everything back. A fall plays the knock-down on foot.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    [DisallowMultipleComponent]
    public sealed class HeroMountPresenter : MonoBehaviour
    {
        private static readonly int MountStateHash = Animator.StringToHash("Mount");
        private static readonly int DismountStateHash = Animator.StringToHash("Dismount");
        /// <summary>Body collider on horseback: horse and rider (the motor's CharacterController).</summary>
        private const float RidingRadius = 0.6f, RidingHeight = 2.6f;
        private const float CameraRise = 0.8f;

        private World _world;
        private Entity _hero = Entity.Null;
        private Animator _animator;
        private Transform _hips;
        private uint _appliedRevision;
        private HeroHorseVisual _horse;
        private Quaternion _seatCorrection = Quaternion.identity;

        private RuntimeAnimatorController _footController;
        private readonly List<Behaviour> _paused = new();
        private GameObject _hiddenOffHand;
        private CharacterController _controller;
        private float _footRadius, _footHeight;
        private Vector3 _footCenter;
        private NavMeshAgent _agent;
        private float _footAgentRadius, _footAgentHeight, _footAgentAngular, _footAgentAcceleration;
        private uint _appliedJump;
        private Transform _follow;
        private Vector3 _footFollow;
        private World _configWorld;
        private EntityQuery _config;
        private RuntimeAnimatorController _gaitChecked;
        private bool _riderHasGait;

        public bool Riding => _horse != null;
        public HeroHorseVisual Horse => _horse;

        public void Bind(World world, Entity hero)
        {
            if (_world == world && _hero == hero) return;
            if (Riding) Dismount(HeroMountEnd.None);
            _world = world;
            _hero = hero;
            _appliedRevision = uint.MaxValue;   // apply the current state at once
            _appliedJump = world != null && world.IsCreated && world.EntityManager.Exists(hero) && world.EntityManager.HasComponent<HeroMountComponent>(hero)
                ? world.EntityManager.GetComponentData<HeroMountComponent>(hero).poseJumpRevision : 0;
            _animator = GetComponent<Animator>();
        }

        private void Update()
        {
            if (_world == null || !_world.IsCreated || _hero == Entity.Null) return;
            var em = _world.EntityManager;
            if (!em.Exists(_hero) || !em.HasComponent<HeroMountComponent>(_hero))
            {
                if (Riding) Dismount(HeroMountEnd.None);
                return;
            }
            var mount = em.GetComponentData<HeroMountComponent>(_hero);
            JumpAgent(em, mount);
            if (mount.revision != _appliedRevision)
            {
                _appliedRevision = mount.revision;
                Show(em, mount);
            }
            if (!Riding) return;

            var tuning = Tuning(em);
            float3 velocity = em.HasComponent<HeroMotorStateComponent>(_hero) ? em.GetComponentData<HeroMotorStateComponent>(_hero).velocity
                : _agent != null && _agent.enabled ? (float3)_agent.velocity : float3.zero;
            float speed = mount.state == HeroMountState.Mounted ? math.length(velocity.xz) : 0f;
            var (blend, scale) = HeroHorseVisual.Gait(speed, tuning.trotSpeed, tuning.gallopSpeed);
            _horse.SetGait(blend, scale);
            if (RiderHasGait())
            {
                _animator.SetFloat(AnimationHashes.MountGaitBlend, blend);
                _animator.SetFloat(AnimationHashes.MountGaitScale, scale);
            }

            // The local rider faces the heading the horse turned to (remote ones turn with their agent).
            if (mount.state == HeroMountState.Mounted && em.HasComponent<IsLocalPlayer>(_hero) && em.HasComponent<HeroMoveIntent>(_hero))
            {
                float3 heading = em.GetComponentData<HeroMoveIntent>(_hero).Direction;
                heading.y = 0f;
                if (math.lengthsq(heading) > 1e-6f) transform.rotation = Quaternion.LookRotation((Vector3)heading, Vector3.up);
            }
        }

        private void LateUpdate()
        {
            // The rider's clips are in the seat's space: carry the animated pelvis from the root to the saddle.
            if (!Riding || _hips == null || _horse.Saddle == null) return;
            Quaternion seat = _horse.Saddle.rotation * _seatCorrection;
            Quaternion inverseRoot = Quaternion.Inverse(transform.rotation);
            Vector3 local = inverseRoot * (_hips.position - transform.position);
            _hips.SetPositionAndRotation(_horse.Saddle.position + seat * local, seat * (inverseRoot * _hips.rotation));
        }

        private void OnDestroy()
        {
            if (_horse != null) Destroy(_horse.gameObject);
        }

        /// <summary>A hero moved by its NavMeshAgent (AI or remote) jumps here, as the local one does in its motor.</summary>
        private void JumpAgent(EntityManager em, in HeroMountComponent mount)
        {
            if (mount.poseJumpRevision == _appliedJump) return;
            _appliedJump = mount.poseJumpRevision;
            if (em.HasComponent<IsLocalPlayer>(_hero)) return;
            var agent = GetComponent<NavMeshAgent>();
            if (!(agent != null && agent.enabled && agent.isOnNavMesh && agent.Warp(mount.poseJumpPosition)))
                transform.position = mount.poseJumpPosition;
            transform.rotation = mount.poseJumpRotation;
        }

        private void Show(EntityManager em, in HeroMountComponent mount)
        {
            if (!mount.Riding)
            {
                if (Riding) Dismount(mount.lastEnd);
                return;
            }
            if (!Riding && !Mount(em)) return;
            if (mount.state == HeroMountState.Mounting) _animator.Play(MountStateHash, 0, Mathf.Clamp01(mount.elapsed / Tuning(em).mountSeconds));
            else if (mount.state == HeroMountState.Dismounting) _animator.CrossFadeInFixedTime(DismountStateHash, 0.15f, 0);
        }

        private bool Mount(EntityManager em)
        {
            var prefab = Resources.Load<HeroHorseVisual>(HeroHorseVisual.ResourcePath);
            if (prefab == null || _animator == null)
            {
                Debug.LogWarning($"[HeroMountPresenter] No horse visual ({HeroHorseVisual.ResourcePath}) or no Animator on {name}.");
                return false;
            }
            var heroClass = em.HasComponent<HeroClassComponent>(_hero) ? em.GetComponentData<HeroClassComponent>(_hero).heroClass : HeroClass.SwordAndShield;
            var rider = prefab.RiderFor(heroClass);
            if (rider == null) return false;

            _horse = Instantiate(prefab, transform, false);
            _horse.name = "HeroHorse";
            _horse.transform.localPosition = Vector3.zero;
            _horse.transform.localRotation = Quaternion.identity;
            _horse.SetReins(heroClass);
            // Upright and facing the horse's front with the horse at rest (its bones have not been animated yet).
            _seatCorrection = _horse.Saddle != null ? Quaternion.Inverse(_horse.Saddle.rotation) * _horse.transform.rotation : Quaternion.identity;

            _footController = _animator.runtimeAnimatorController;
            _animator.runtimeAnimatorController = rider;
            _hips = _animator.isHuman ? _animator.GetBoneTransform(HumanBodyBones.Hips) : null;

            _paused.Clear();
            foreach (var driver in new Behaviour[]
                     {
                         GetComponent<SamplePlayerAnimationController_ECS>(), GetComponent<EcsAnimationInputAdapter>(),
                         GetComponent<RemoteHeroAnimationDriver>(),
                     })
                if (driver != null && driver.enabled) { driver.enabled = false; _paused.Add(driver); }

            if (heroClass == HeroClass.SwordAndShield)
            {
                var offHand = FindDeep(transform, "OffWeaponCarrier");
                if (offHand != null && offHand.gameObject.activeSelf) { _hiddenOffHand = offHand.gameObject; _hiddenOffHand.SetActive(false); }
            }

            _controller = GetComponent<CharacterController>();
            if (_controller != null)
            {
                _footRadius = _controller.radius; _footHeight = _controller.height; _footCenter = _controller.center;
                _controller.radius = RidingRadius; _controller.height = RidingHeight; _controller.center = new Vector3(0f, RidingHeight * 0.5f, 0f);
            }
            _agent = GetComponent<NavMeshAgent>();
            if (_agent != null)
            {
                _footAgentRadius = _agent.radius; _footAgentHeight = _agent.height;
                _footAgentAngular = _agent.angularSpeed; _footAgentAcceleration = _agent.acceleration;
                var tuning = Tuning(em);
                _agent.radius = 0.9f; _agent.height = RidingHeight;
                _agent.angularSpeed = tuning.turnRate; _agent.acceleration = tuning.acceleration;
            }
            _follow = transform.Find("CameraFollowPoint");
            if (_follow != null) { _footFollow = _follow.localPosition; _follow.localPosition = _footFollow + Vector3.up * CameraRise; }
            return true;
        }

        private void Dismount(HeroMountEnd why)
        {
            if (_horse != null) Destroy(_horse.gameObject);
            _horse = null;
            _hips = null;
            if (_animator != null && _footController != null) _animator.runtimeAnimatorController = _footController;
            _footController = null;
            foreach (var driver in _paused) if (driver != null) driver.enabled = true;
            _paused.Clear();
            if (_hiddenOffHand != null) _hiddenOffHand.SetActive(true);
            _hiddenOffHand = null;
            if (_controller != null) { _controller.radius = _footRadius; _controller.height = _footHeight; _controller.center = _footCenter; }
            _controller = null;
            if (_agent != null)
            {
                _agent.radius = _footAgentRadius; _agent.height = _footAgentHeight;
                _agent.angularSpeed = _footAgentAngular; _agent.acceleration = _footAgentAcceleration;
            }
            _agent = null;
            if (_follow != null) _follow.localPosition = _footFollow;
            _follow = null;
            Fall(why);
        }

        /// <summary>Thrown off or the horse killed: the knock-down (or the launch that threw it) on foot; dead: the death.</summary>
        private void Fall(HeroMountEnd why)
        {
            if (_animator == null || _animator.runtimeAnimatorController == null) return;
            bool ecs = _world != null && _world.IsCreated;
            if (why == HeroMountEnd.HeroDied)
            {
                if (AnimationHashes.HasParameter(_animator, AnimationHashes.TriggerDeath)) _animator.SetTrigger(AnimationHashes.TriggerDeath);
                return;
            }
            if (why != HeroMountEnd.HorseKilled && why != HeroMountEnd.ThrownOff) return;
            var reaction = HitReaction.Knockdown;
            if (why == HeroMountEnd.ThrownOff && ecs && _world.EntityManager.Exists(_hero) && _world.EntityManager.HasComponent<ResilienceComponent>(_hero))
            {
                var thrown = _world.EntityManager.GetComponentData<ResilienceComponent>(_hero).reaction;
                if (HeroMountRules.ThrowsOff(thrown)) reaction = thrown;
            }
            if (AnimationHashes.HasParameter(_animator, AnimationHashes.HitReactionType)
                && AnimationHashes.HasParameter(_animator, AnimationHashes.TriggerHitReaction))
            {
                _animator.SetInteger(AnimationHashes.HitReactionType, (int)reaction);
                _animator.SetTrigger(AnimationHashes.TriggerHitReaction);
            }
        }

        private MountTuning Tuning(EntityManager em)
        {
            if (_configWorld != _world)
            {
                _configWorld = _world;
                _config = em.CreateEntityQuery(ComponentType.ReadOnly<HeroGameplayConfigComponent>());
            }
            return _config.TryGetSingleton<HeroGameplayConfigComponent>(out var c) ? c.mount.OrDefault() : MountTuning.Default;
        }

        /// <summary>The rider controller has the gait parameters (checked once per controller).</summary>
        private bool RiderHasGait()
        {
            var controller = _animator != null ? _animator.runtimeAnimatorController : null;
            if (controller != _gaitChecked)
            {
                _gaitChecked = controller;
                _riderHasGait = controller != null && AnimationHashes.HasParameter(_animator, AnimationHashes.MountGaitBlend);
            }
            return _riderHasGait;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
