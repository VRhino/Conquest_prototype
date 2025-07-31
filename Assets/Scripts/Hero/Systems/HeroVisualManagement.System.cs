using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using ConquestTactics.Visual;
using System.Collections.Generic;
using Data.Items;

/// <summary>
/// Sistema que gestiona la instanciación y sincronización de los GameObjects visuales
/// para las entidades del héroe. Se ejecuta después del HeroSpawnSystem para crear
/// los visuales cuando se instancia una nueva entidad de héroe.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(HeroSpawnSystem))]
public partial class HeroVisualManagementSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        
        // Crear visuales para entidades recién spawneadas
        foreach (var (spawn, transform, entity) in
                 SystemAPI.Query<RefRO<HeroSpawnComponent>,
                                 RefRO<LocalTransform>>()
                        .WithAll<IsLocalPlayer>()
                        .WithNone<HeroVisualInstance>()
                        .WithEntityAccess())
        {
            // Asegurar que la entidad esté completamente spawneada
            if (!spawn.ValueRO.hasSpawned)
            {
                Debug.Log($"[HeroVisualManagementSystem] Entity {entity} not yet spawned, skipping visual creation");
                continue;
            }
            
                // Usar el id del prefab visual desde HeroSpawnComponent
                CreateVisualForEntity(entity, spawn.ValueRO.visualPrefabId.ToString(), transform.ValueRO, ecb);
        }
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
    
    /// <summary>
    /// Crea un GameObject visual para la entidad especificada.
    /// </summary>
    /// <param name="entity">Entidad para la que crear el visual</param>
    /// <param name="visualPrefabId">ID del prefab visual</param>
    /// <param name="transform">Transform inicial de la entidad</param>
    /// <param name="ecb">EntityCommandBuffer para agregar componentes</param>
    private void CreateVisualForEntity(Entity entity, string visualPrefabId, 
        LocalTransform transform, EntityCommandBuffer ecb)
    {
        // Buscar el prefab visual usando el id
        GameObject visualPrefab = FindVisualPrefabGameObject(visualPrefabId);
        
        if (visualPrefab == null)
        {
            Debug.LogWarning($"[HeroVisualManagementSystem] GameObject del prefab visual no encontrado para id: {visualPrefabId}");
            return;
        }
        
        // Instanciar el GameObject visual
        GameObject visualInstance = Object.Instantiate(visualPrefab);
        visualInstance.transform.position = transform.Position;
        visualInstance.transform.rotation = transform.Rotation;
        visualInstance.transform.localScale = Vector3.one * transform.Scale;

        try
        {
            // Aplicar la personalización visual del héroe seleccionado
            ApplyHeroVisualCustomization(visualInstance);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[HeroVisualManagementSystem] Error applying visual customization: {ex.Message}\n{ex.StackTrace}");
        }

        // Configurar el script de sincronización
        EntityVisualSync syncScript = visualInstance.GetComponent<EntityVisualSync>();
        if (syncScript == null)
        {
            syncScript = visualInstance.AddComponent<EntityVisualSync>();
        }

        syncScript.SetHeroEntity(entity);
        // Marcar la entidad como teniendo un visual instanciado (siempre, incluso si hubo error en customización)
        ecb.AddComponent(entity, new HeroVisualInstance
        {
            visualInstanceId = visualInstance.GetInstanceID()
        });
    }

    /// <summary>
    /// Aplica la personalización visual del HeroData seleccionado al dummy visual instanciado.
    /// </summary>
    /// <param name="visualInstance">GameObject visual del héroe</param>
    private void ApplyHeroVisualCustomization(GameObject visualInstance)
    {
        var heroData = PlayerSessionService.SelectedHero;
        if (heroData == null)
        {
            return;
        }
        var avatarPartDatabase = Resources.Load<Data.Avatar.AvatarPartDatabase>("Data/Avatar/AvatarPartDatabase");
        var itemDB = Resources.Load<ItemDatabase>("Data/Items/ItemDatabase");
        if (avatarPartDatabase == null || itemDB == null)
        {
            return;
        }

        // 1) Aplicar partes base visuales
        var baseVisualPartIds = new List<string>();
        if (!string.IsNullOrEmpty(heroData.avatar.headId)) baseVisualPartIds.Add(heroData.avatar.headId);
        if (!string.IsNullOrEmpty(heroData.avatar.hairId)) baseVisualPartIds.Add(heroData.avatar.hairId);
        if (!string.IsNullOrEmpty(heroData.avatar.beardId)) baseVisualPartIds.Add(heroData.avatar.beardId);
        if (!string.IsNullOrEmpty(heroData.avatar.eyebrowId)) baseVisualPartIds.Add(heroData.avatar.eyebrowId);

        Data.Avatar.AvatarVisualUtils.ResetModularDummyToBase(
            visualInstance.transform,
            avatarPartDatabase,
            baseVisualPartIds,
            heroData.gender == "Male" ? Gender.Male : Gender.Female
        );

        // 2) Aplicar equipo funcional
        var equipmentIds = new List<string> {
            heroData.equipment.weaponId,
            heroData.equipment.helmetId,
            heroData.equipment.torsoId,
            heroData.equipment.glovesId,
            heroData.equipment.pantsId
        };
        foreach (var itemId in equipmentIds)
        {
            if (!string.IsNullOrEmpty(itemId))
            {
                var itemData = itemDB.GetItemDataById(itemId);
                if (itemData != null && !string.IsNullOrEmpty(itemData.visualPartId))
                {
                    Data.Avatar.AvatarVisualUtils.ToggleArmorVisibilityByAvatarPartId(
                        visualInstance.transform,
                        avatarPartDatabase,
                        itemData.visualPartId,
                        heroData.gender == "Male" ? Gender.Male : Gender.Female
                    );
                }
            }
        }
    }
    
    /// <summary>
    /// Busca el prefab GameObject visual usando el VisualPrefabRegistry.
    /// </summary>
    /// <param name="prefabId">ID del prefab a buscar</param>
    /// <returns>GameObject del prefab visual o null si no se encuentra</returns>
    private GameObject FindVisualPrefabGameObject(string prefabId)
    {
        // Usar el registro de prefabs visuales
        VisualPrefabRegistry registry = VisualPrefabRegistry.Instance;
        GameObject prefab = registry.GetPrefab(prefabId);
        return prefab;
    }
}
