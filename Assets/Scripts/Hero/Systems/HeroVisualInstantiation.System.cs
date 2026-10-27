using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AI;
using ConquestTactics.Visual;
using System.Collections.Generic;

/// <summary>
/// Instancia el GameObject visual para cada entidad héroe recién spawneada.
/// Configura EntityVisualSync, weapon hitbox y NavMeshAgent.
/// La apariencia de avatar y equipment la aplica HeroVisualAppearanceSystem.
/// Cuando HeroDeathPresentationComponent pide ocultar un héroe muerto, destruye su visual (sin colisión
/// ni agente en el campo) y lo vuelve a crear en la pose de reaparición.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(HeroSpawnSystem))]
public partial class HeroVisualInstantiationSystem : SystemBase
{
    private readonly List<(Entity entity, NavMeshAgent agent)> _pendingNavAgents = new();
    protected override void OnCreate()
    {
        RequireAnyForUpdate(
            GetEntityQuery(ComponentType.ReadOnly<HeroSpawnComponent>(),
                ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<TeamComponent>(),
                ComponentType.Exclude<HeroVisualInstance>(), ComponentType.Exclude<IsDeadComponent>()),
            // Only dead heroes that still have a body: alive frames keep the system idle.
            GetEntityQuery(ComponentType.ReadOnly<HeroVisualInstance>(),
                ComponentType.ReadOnly<HeroDeathPresentationComponent>(),
                ComponentType.ReadOnly<IsDeadComponent>()));
    }

    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        ReleaseHiddenVisuals(ecb);
        var pendingNavAgents = _pendingNavAgents;
        pendingNavAgents.Clear();
        float navSampleRadius = SystemAPI.HasSingleton<SquadSpawnConfigComponent>()
            ? Mathf.Max(0.01f, SystemAPI.GetSingleton<SquadSpawnConfigComponent>()
                .navMeshDestinationSampleRadius)
            : 2f;

        foreach (var (spawn, transform, team, entity) in
                 SystemAPI.Query<RefRO<HeroSpawnComponent>, RefRO<LocalTransform>, RefRO<TeamComponent>>()
                          .WithNone<HeroVisualInstance, IsDeadComponent>()
                          .WithEntityAccess())
        {
            if (!spawn.ValueRO.hasSpawned) continue;
            if (SystemAPI.HasComponent<HeroDeathPresentationComponent>(entity)
                && SystemAPI.GetComponent<HeroDeathPresentationComponent>(entity).visualHidden) continue;

            bool isLocal = SystemAPI.HasComponent<IsLocalPlayer>(entity);
            string baseId = spawn.ValueRO.visualPrefabId.ToString();
            string prefabKey = isLocal ? baseId : baseId + "_Remote";
            CreateVisualForEntity(entity, prefabKey, transform.ValueRO, ecb, isLocal,
                pendingNavAgents, team.ValueRO.value, navSampleRadius);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();

        // Adjuntar NavMeshAgent como componente manejado después del playback
        foreach (var (entity, agent) in pendingNavAgents)
        {
            EntityManager.AddComponentObject(entity, agent);
            // Remote heroes keep their visual/NavMesh sync in EntityVisualSync.
            // Local heroes publish through LocalHeroCharacterMotor instead.
            EntityManager.AddComponentData(entity, new NavAgentComponent { syncPositionFromNavMesh = false });
        }
    }

    /// <summary>
    /// Removes the visual of every hero whose death presentation asks to hide it, so the body leaves
    /// the scene with its collider, hitboxes and NavMeshAgent. The creation pass rebuilds it after the respawn.
    /// </summary>
    private void ReleaseHiddenVisuals(EntityCommandBuffer ecb)
    {
        foreach (var (presentation, entity) in
                 SystemAPI.Query<RefRO<HeroDeathPresentationComponent>>()
                          .WithAll<HeroVisualInstance, IsDeadComponent>()
                          .WithEntityAccess())
        {
            if (!presentation.ValueRO.visualHidden) continue;

            if (VisualSyncUtility.TryGetVisual(World, entity, out var visual) && visual != null)
            {
                // Inactive at once so its collider and agent stop acting this frame; destroyed at frame end.
                visual.gameObject.SetActive(false);
                Object.Destroy(visual.gameObject);
            }
            ecb.RemoveComponent<HeroVisualInstance>(entity);
            ecb.RemoveComponent<HeroVisualAppearanceApplied>(entity);
            ecb.RemoveComponent<NavAgentComponent>(entity);
            if (EntityManager.HasComponent<NavMeshAgent>(entity))
                ecb.RemoveComponent<NavMeshAgent>(entity);
        }
    }

    private void CreateVisualForEntity(Entity entity, string visualPrefabId,
        LocalTransform transform, EntityCommandBuffer ecb, bool isLocalPlayer,
        List<(Entity, NavMeshAgent)> pendingNavAgents, Team team, float navSampleRadius)
    {
        GameObject visualPrefab = VisualPrefabRegistry.Instance.GetPrefab(visualPrefabId);
        if (visualPrefab == null)
        {
            Debug.LogWarning($"[HeroVisualInstantiationSystem] Prefab no encontrado para id: {visualPrefabId}");
            return;
        }

        if (visualPrefab.GetComponentInChildren<ShieldHitboxBehaviour>(true) != null
            && !SystemAPI.HasSingleton<ShieldConfigComponent>())
        {
            Debug.LogError($"[HeroVisualInstantiationSystem] ShieldConfigComponent missing for {visualPrefabId}; visual creation deferred.");
            return;
        }

        GameObject visualInstance = Object.Instantiate(visualPrefab);
        visualInstance.transform.position = transform.Position;
        visualInstance.transform.rotation = transform.Rotation;
        visualInstance.transform.localScale = Vector3.one * transform.Scale;

        // Asignar layer por equipo para colisiones selectivas entre teams
        string heroLayerName = team == Team.TeamB ? "Heroes_B" : "Heroes_A";
        int heroesLayer = LayerMask.NameToLayer(heroLayerName);
        if (heroesLayer >= 0)
            SetLayerRecursively(visualInstance, heroesLayer);

        // Asignar tag "Player" al héroe local para que HeroDetail3DPreview lo pueda encontrar
        if (isLocalPlayer)
            visualInstance.tag = GameTags.Player;

        var syncScript = VisualSyncUtility.SetupVisualSync(visualInstance);
        syncScript.SetHeroEntity(entity);

        // Wire weapon hitbox to this hero entity
        var hitboxBehaviour = visualInstance.GetComponentInChildren<WeaponHitboxBehaviour>(true);
        if (hitboxBehaviour != null)
            hitboxBehaviour.ownerUnit = entity;

        // Wire ShieldHitboxBehaviour → add UnitShieldComponent if hero prefab has shield
        var shieldBehaviour = visualInstance.GetComponentInChildren<ShieldHitboxBehaviour>(true);
        if (shieldBehaviour != null)
        {
            shieldBehaviour.ownerUnit = entity;

            var shieldCfg   = SystemAPI.GetSingleton<ShieldConfigComponent>();
            float maxBlock  = shieldBehaviour.maxBlockOverride  > 0f ? shieldBehaviour.maxBlockOverride  : shieldCfg.defaultMaxBlock;
            float regenRate = shieldBehaviour.regenRateOverride > 0f ? shieldBehaviour.regenRateOverride : shieldCfg.defaultRegenRate;

            ecb.AddComponent(entity, new UnitShieldComponent
            {
                currentBlock      = maxBlock,
                maxBlock          = maxBlock,
                regenRate         = regenRate,
                orientation       = shieldBehaviour.orientation,
                breakStunDuration = shieldCfg.defaultBreakStunDuration
            });
        }

        // Heroes remotos: NavMeshAgent viene del prefab Player_Remote — forzar posición al spawn
        if (!isLocalPlayer)
        {
            var agent = visualInstance.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                if (!TryPlaceRemoteAgent(agent, visualInstance.transform.position, navSampleRadius))
                    Debug.LogWarning($"[HeroVisualInstantiationSystem] Remote hero {entity.Index} could not be placed on NavMesh.");
                pendingNavAgents.Add((entity, agent));
            }
        }

        ecb.AddComponent(entity, new HeroVisualInstance
        {
            visualInstanceId = visualInstance.GetEntityId()
        });
    }

    public static bool TryPlaceRemoteAgent(NavMeshAgent agent, Vector3 requestedPosition,
        float sampleRadius)
    {
        if (agent == null || !agent.enabled) return false;
        if (agent.Warp(requestedPosition)) return true;
        return UnitNavMeshSystem.TrySampleDestination(agent, requestedPosition,
                Mathf.Max(0.01f, sampleRadius), out var resolved)
            && agent.Warp(resolved);
    }

    private static void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}
