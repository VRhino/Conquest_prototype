using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Conquest.SettlementPreview
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateBefore(typeof(CameraBootstrapSystem))]
    public partial class SettlementHeroLifecycleSystem : SystemBase
    {
        private Entity hero;
        private SettlementWalkMode owner;
        protected override void OnUpdate()
        {
            var request = SettlementWalkMode.Instance;
            if (hero != Entity.Null && (!request || !request.Requested || request != owner)) Release();
            if (!request || !request.Requested) return;
            if (hero != Entity.Null && EntityManager.Exists(hero))
            {
                if (EntityManager.HasComponent<HeroVisualInstance>(hero) && !request.Ready)
                {
                    var id = EntityManager.GetComponentData<HeroVisualInstance>(hero).visualInstanceId;
                    foreach (var visual in Object.FindObjectsByType<ConquestTactics.Visual.EntityVisualSync>())
                        if (visual.gameObject.GetEntityId() == id) visual.DebugLogging = false;
                    request.PresentHero();
                }
                return;
            }
            using var local = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<IsLocalPlayer>());
            if (!local.IsEmptyIgnoreFilter) { request.Fail("Ya existe otro héroe local. Abre SettlementPreview sola."); return; }
            if (!SystemAPI.TryGetSingleton<SettlementHeroPrefab>(out var prefab)) return;
            // Existing visual and stamina systems require these baked configurations.
            if (!SystemAPI.HasSingleton<HeroGameplayConfigComponent>() || !SystemAPI.HasSingleton<ShieldConfigComponent>()) return;
            hero = EntityManager.Instantiate(prefab.Value); owner = request;
            EntityManager.AddComponent<SettlementWalkingHero>(hero);
            if (EntityManager.HasComponent<HeroSquadSelectionComponent>(hero)) EntityManager.RemoveComponent<HeroSquadSelectionComponent>(hero);
            EntityManager.SetComponentData(hero, LocalTransform.FromPositionRotationScale(request.SpawnPosition, request.SpawnRotation, 1));
            var spawn = EntityManager.GetComponentData<HeroSpawnComponent>(hero);
            spawn.hasSpawned = true; spawn.spawnPosition = request.SpawnPosition; spawn.spawnRotation = request.SpawnRotation;
            EntityManager.SetComponentData(hero, spawn);
            var life = EntityManager.GetComponentData<HeroLifeComponent>(hero); life.isAlive = true; EntityManager.SetComponentData(hero, life);
            EntityManager.AddComponentData(hero, new HeroMoveIntent { Direction = float3.zero, Speed = 0 });
#if UNITY_EDITOR
            EntityManager.SetName(hero, "Settlement local hero");
#endif
        }
        private void Release()
        {
            if (EntityManager.Exists(hero))
            {
                // Destroy only camera state targeting OUR hero; never clear a whole World.
                using var query = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<CameraTargetComponent>());
                using var cameras = query.ToEntityArray(Allocator.Temp);
                foreach (var camera in cameras)
                    if (EntityManager.GetComponentData<CameraTargetComponent>(camera).followTarget == hero) EntityManager.DestroyEntity(camera);
                if (EntityManager.HasComponent<HeroVisualInstance>(hero))
                {
                    var id = EntityManager.GetComponentData<HeroVisualInstance>(hero).visualInstanceId;
                    foreach (var visual in Object.FindObjectsByType<ConquestTactics.Visual.EntityVisualSync>())
                        if (visual.gameObject.GetEntityId() == id) { visual.gameObject.SetActive(false); Object.Destroy(visual.gameObject); }
                }
                EntityManager.DestroyEntity(hero);
            }
            hero = Entity.Null; owner = null;
        }
        protected override void OnDestroy() { Release(); }
    }

    // Preserve existing locomotion input, but do not let the walk-only preview attack or interact.
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(HeroInputSystem))]
    [UpdateBefore(typeof(HeroMovementSystem))]
    [UpdateBefore(typeof(HeroAttackSystem))]
    [UpdateBefore(typeof(HeroStaminaSystem))]
    public partial class SettlementWalkInputFilterSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            foreach (var (input, entity) in SystemAPI.Query<RefRW<HeroInputComponent>>().WithAll<SettlementWalkingHero>().WithEntityAccess())
            {
                var value = input.ValueRO;
                value.IsAttackPressed = false; value.UseSkill1 = false; value.UseSkill2 = false; value.UseUltimate = false;
                if (!SettlementWalkMode.Instance || !SettlementWalkMode.Instance.Ready) { value.MoveInput = float2.zero; value.IsSprintPressed = false; }
                input.ValueRW = value;
                if (EntityManager.HasComponent<PlayerInteractionComponent>(entity))
                {
                    var interaction = EntityManager.GetComponentData<PlayerInteractionComponent>(entity); interaction.interactPressed = false;
                    EntityManager.SetComponentData(entity, interaction);
                }
            }
        }
    }
}
