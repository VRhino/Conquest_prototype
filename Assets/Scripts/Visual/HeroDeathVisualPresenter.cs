using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace ConquestTactics.Visual
{
    /// <summary>
    /// Hides a hero's renderers while HeroDeathPresentationComponent.visualHidden is set and restores them
    /// after the respawn. Runs in LateUpdate so the local motor has already applied the respawn pose.
    /// Read-only: it never writes ECS.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroDeathVisualPresenter : MonoBehaviour
    {
        private World _world;
        private Entity _heroEntity = Entity.Null;
        private bool _hidden;
        private readonly List<Renderer> _hiddenRenderers = new();

        public bool IsHidden => _hidden;

        public void Bind(World world, Entity heroEntity)
        {
            _world = world;
            _heroEntity = heroEntity;
        }

        private void LateUpdate()
        {
            if (_world == null || !_world.IsCreated || _heroEntity == Entity.Null)
                return;
            var entityManager = _world.EntityManager;
            if (!entityManager.Exists(_heroEntity)
                || !entityManager.HasComponent<HeroDeathPresentationComponent>(_heroEntity))
                return;

            bool hide = entityManager.GetComponentData<HeroDeathPresentationComponent>(_heroEntity).visualHidden;
            if (hide != _hidden)
                SetHidden(hide);
        }

        private void OnDisable()
        {
            if (_hidden)
                SetHidden(false);
        }

        private void SetHidden(bool hide)
        {
            _hidden = hide;
            if (hide)
            {
                // Remember only what was on, so renderers the visual keeps off stay off after the respawn.
                _hiddenRenderers.Clear();
                foreach (var meshRenderer in GetComponentsInChildren<Renderer>(true))
                {
                    if (!meshRenderer.enabled)
                        continue;
                    meshRenderer.enabled = false;
                    _hiddenRenderers.Add(meshRenderer);
                }
                return;
            }

            foreach (var meshRenderer in _hiddenRenderers)
            {
                if (meshRenderer != null)
                    meshRenderer.enabled = true;
            }
            _hiddenRenderers.Clear();
        }
    }
}
