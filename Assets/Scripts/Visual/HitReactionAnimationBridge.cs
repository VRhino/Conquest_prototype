using Unity.Entities;
using UnityEngine;

namespace ConquestTactics.Visual
{
    /// <summary>
    /// Read-only bridge from <see cref="ResilienceComponent.reaction"/> to the Animator (L1-09): when the reaction revision
    /// changes it sets <c>HitReactionType</c> and fires <c>TriggerHitReaction</c>, once per reaction, without writing to
    /// ECS (same pattern as <see cref="HeroDeathAnimationBridge"/>). A visual bound after a reaction does not replay it,
    /// and a dead body does not react. Controllers without the parameters are left untouched.
    /// </summary>
    public sealed class HitReactionAnimationBridge
    {
        private Entity _entity = Entity.Null;
        private uint _seenRevision;
        private RuntimeAnimatorController _checkedController;
        private bool _hasParameters;

        /// <summary>Heroes: reads the entity's resilience.</summary>
        public void Apply(EntityManager entityManager, Entity entity, Animator animator)
        {
            if (animator == null || entity == Entity.Null || !entityManager.Exists(entity)
                || !entityManager.HasComponent<ResilienceComponent>(entity))
                return;
            var resilience = entityManager.GetComponentData<ResilienceComponent>(entity);
            Apply(entity, resilience.reactionRevision, resilience.reaction, entityManager.HasComponent<IsDeadComponent>(entity), animator);
        }

        /// <summary>Soldiers: the values their animation adapter already read this frame.</summary>
        public void Apply(Entity entity, uint revision, HitReaction reaction, bool dead, Animator animator)
        {
            if (animator == null || entity == Entity.Null)
                return;
            if (_entity != entity)
            {
                _entity = entity;
                _seenRevision = revision;
                return;
            }
            if (revision == _seenRevision)
                return;
            _seenRevision = revision;
            if (dead || reaction == HitReaction.None || !HasParameters(animator))
                return;
            animator.SetInteger(AnimationHashes.HitReactionType, (int)reaction);
            animator.SetTrigger(AnimationHashes.TriggerHitReaction);
        }

        private bool HasParameters(Animator animator)
        {
            var controller = animator.runtimeAnimatorController;
            if (controller == _checkedController)
                return _hasParameters;
            _checkedController = controller;
            bool trigger = false, type = false;
            if (controller != null)
                foreach (var parameter in animator.parameters)
                {
                    if (parameter.nameHash == AnimationHashes.TriggerHitReaction && parameter.type == AnimatorControllerParameterType.Trigger)
                        trigger = true;
                    else if (parameter.nameHash == AnimationHashes.HitReactionType && parameter.type == AnimatorControllerParameterType.Int)
                        type = true;
                }
            _hasParameters = trigger && type;
            return _hasParameters;
        }
    }
}
