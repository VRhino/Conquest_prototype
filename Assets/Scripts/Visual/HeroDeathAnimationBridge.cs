using Unity.Entities;
using UnityEngine;

namespace ConquestTactics.Visual
{
    /// <summary>
    /// Read-only bridge from HeroDeathPresentationComponent to the Animator: fires TriggerDeath once per
    /// death (by revision, so nothing is written back to ECS) and holds IsDead until the respawn.
    /// Controllers without these parameters are left untouched.
    /// </summary>
    public sealed class HeroDeathAnimationBridge
    {
        private Entity _entity = Entity.Null;
        private uint _seenRevision;
        private RuntimeAnimatorController _checkedController;
        private bool _hasTrigger;
        private bool _hasIsDead;

        public void Apply(EntityManager entityManager, Entity heroEntity, Animator animator)
        {
            if (animator == null || !entityManager.HasComponent<HeroDeathPresentationComponent>(heroEntity))
                return;

            var presentation = entityManager.GetComponentData<HeroDeathPresentationComponent>(heroEntity);
            if (_entity != heroEntity)
            {
                // A visual bound after the death does not replay it.
                _entity = heroEntity;
                _seenRevision = presentation.deathRevision;
            }

            RefreshParameters(animator);
            if (_hasIsDead)
                animator.SetBool(AnimationHashes.IsDead, presentation.isDead);
            if (presentation.deathRevision == _seenRevision)
                return;
            _seenRevision = presentation.deathRevision;
            if (_hasTrigger && presentation.isDead)
                animator.SetTrigger(AnimationHashes.TriggerDeath);
        }

        private void RefreshParameters(Animator animator)
        {
            var controller = animator.runtimeAnimatorController;
            if (controller == _checkedController)
                return;
            _checkedController = controller;
            _hasTrigger = false;
            _hasIsDead = false;
            if (controller == null)
                return;
            foreach (var parameter in animator.parameters)
            {
                if (parameter.nameHash == AnimationHashes.TriggerDeath
                    && parameter.type == AnimatorControllerParameterType.Trigger)
                    _hasTrigger = true;
                else if (parameter.nameHash == AnimationHashes.IsDead
                         && parameter.type == AnimatorControllerParameterType.Bool)
                    _hasIsDead = true;
            }
        }
    }
}
