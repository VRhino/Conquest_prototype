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
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(HeroSpawnSystem))]
public partial class HeroVisualInstantiationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var pendingNavAgents = new List<(Entity entity, NavMeshAgent agent)>();

        foreach (var (spawn, transform, entity) in
                 SystemAPI.Query<RefRO<HeroSpawnComponent>, RefRO<LocalTransform>>()
                          .WithNone<HeroVisualInstance>()
                          .WithEntityAccess())
        {
            if (!spawn.ValueRO.hasSpawned) continue;

            bool isLocal = SystemAPI.HasComponent<IsLocalPlayer>(entity);
            string baseId = spawn.ValueRO.visualPrefabId.ToString();
            string prefabKey = isLocal ? baseId : baseId + "_Remote";
            CreateVisualForEntity(entity, prefabKey, transform.ValueRO, ecb, isLocal, pendingNavAgents);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();

        // Adjuntar NavMeshAgent como componente manejado después del playback
        foreach (var (entity, agent) in pendingNavAgents)
        {
            EntityManager.AddComponentObject(entity, agent);
            // Heroes: position sync stays in EntityVisualSync (syncPositionFromNavMesh = false)
            EntityManager.AddComponentData(entity, new NavAgentComponent { syncPositionFromNavMesh = false });
        }
    }

    private void CreateVisualForEntity(Entity entity, string visualPrefabId,
        LocalTransform transform, EntityCommandBuffer ecb, bool isLocalPlayer,
        List<(Entity, NavMeshAgent)> pendingNavAgents)
    {
        GameObject visualPrefab = VisualPrefabRegistry.Instance.GetPrefab(visualPrefabId);
        if (visualPrefab == null)
        {
            Debug.LogWarning($"[HeroVisualInstantiationSystem] Prefab no encontrado para id: {visualPrefabId}");
            return;
        }

        GameObject visualInstance = Object.Instantiate(visualPrefab);
        visualInstance.transform.position = transform.Position;
        visualInstance.transform.rotation = transform.Rotation;
        visualInstance.transform.localScale = Vector3.one * transform.Scale;

        // Asignar layer "Heroes" para culling de distancia nativo
        int heroesLayer = LayerMask.NameToLayer("Heroes");
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

        // Heroes remotos: NavMeshAgent viene del prefab Player_Remote — forzar posición al spawn
        if (!isLocalPlayer)
        {
            var agent = visualInstance.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.Warp(visualInstance.transform.position);
                pendingNavAgents.Add((entity, agent));
            }
        }

        ecb.AddComponent(entity, new HeroVisualInstance
        {
            visualInstanceId = visualInstance.GetInstanceID()
        });
    }

    private static void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}
