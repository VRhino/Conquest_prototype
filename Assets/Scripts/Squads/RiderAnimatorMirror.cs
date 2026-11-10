using ConquestTactics.Animation;
using UnityEngine;

namespace ConquestTactics.Visual
{
    /// <summary>
    /// Rider of a mounted soldier (M17): drives its Humanoid Animator from the same <see cref="UnitAnimationAdapter"/>
    /// that drives the horse (<see cref="UnitAnimatorController"/> on the root): gait tree, attack and shot. The rider's
    /// gait clips last as long as the horse's and take the same values, so both play in step; if they drift apart (the
    /// two Animators cull on their own), the rider is put back on the horse's time.
    /// Visual only: reads the adapter, never ECS.
    /// </summary>
    [DefaultExecutionOrder(-59)]
    public sealed class RiderAnimatorMirror : MonoBehaviour
    {
        [Tooltip("Animator del caballo (raíz).")]
        [SerializeField] private Animator _horse;
        [Tooltip("Animator Humanoid del jinete, colgado del hueso Saddle.")]
        [SerializeField] private Animator _rider;
        [Tooltip("Desfase máximo de los andares (tiempo normalizado) antes de volver a poner al jinete al paso del caballo.")]
        [SerializeField, Range(0f, 0.5f)] private float _maxDrift = 0.03f;

        private UnitAnimationAdapter _adapter;
        private RuntimeAnimatorController _checked;
        private bool _hasAttack, _hasAttackSpeed, _hasShoot, _hasGait;

        public Animator Horse => _horse;
        public Animator Rider => _rider;

        public void Bind(Animator horse, Animator rider)
        {
            _horse = horse;
            _rider = rider;
            _checked = null;
        }

        private void Awake() => _adapter = GetComponent<UnitAnimationAdapter>();

        private void Update()
        {
            if (_adapter == null || _rider == null || !_rider.isActiveAndEnabled) return;
            Refresh();
            if (_hasGait)
            {
                _rider.SetFloat(AnimationHashes.MountGaitBlend, _adapter.MountGaitBlend);
                _rider.SetFloat(AnimationHashes.MountGaitScale, _adapter.MountGaitScale);
            }
            if (_hasAttack) _rider.SetBool(AnimationHashes.IsAttacking, _adapter.IsAttacking);
            if (_hasAttackSpeed) _rider.SetFloat(AnimationHashes.AttackSpeed, _adapter.AttackSpeed);
            if (_hasShoot && _adapter.JustShot) _rider.SetTrigger(AnimationHashes.TriggerShoot);
            KeepInStep();
        }

        /// <summary>The rider's gait (base layer) back on the horse's normalized time when they drift apart.</summary>
        private void KeepInStep()
        {
            if (_horse == null || !_horse.isActiveAndEnabled || _horse.IsInTransition(0) || _rider.IsInTransition(0)) return;
            var horse = _horse.GetCurrentAnimatorStateInfo(0);
            var rider = _rider.GetCurrentAnimatorStateInfo(0);
            if (horse.shortNameHash != rider.shortNameHash) return;
            float drift = Mathf.Abs(Mathf.Repeat(horse.normalizedTime, 1f) - Mathf.Repeat(rider.normalizedTime, 1f));
            if (Mathf.Min(drift, 1f - drift) > _maxDrift)
                _rider.Play(rider.shortNameHash, 0, Mathf.Repeat(horse.normalizedTime, 1f));
        }

        private void Refresh()
        {
            var controller = _rider.runtimeAnimatorController;
            if (controller == _checked) return;
            _checked = controller;
            _hasAttack = _hasAttackSpeed = _hasShoot = _hasGait = false;
            if (controller == null) return;
            foreach (var p in _rider.parameters)
            {
                if (p.nameHash == AnimationHashes.IsAttacking) _hasAttack = true;
                else if (p.nameHash == AnimationHashes.AttackSpeed) _hasAttackSpeed = true;
                else if (p.nameHash == AnimationHashes.TriggerShoot) _hasShoot = true;
                else if (p.nameHash == AnimationHashes.MountGaitBlend) _hasGait = true;
            }
        }
    }
}
