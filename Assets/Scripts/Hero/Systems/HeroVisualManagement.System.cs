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
/// También maneja actualizaciones de equipamiento visual en tiempo real.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(HeroSpawnSystem))]
public partial class HeroVisualManagementSystem : SystemBase
{
    private bool _isEventListenerInitialized = false;

    protected override void OnCreate()
    {
        base.OnCreate();
        InitializeEventListeners();
    }

    protected override void OnDestroy()
    {
        UnsubscribeFromEvents();
        base.OnDestroy();
    }

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
        
        if (avatarPartDatabase == null || ItemDatabase.Instance == null)
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
        var equipmentIds = heroData.GetEquipment();
        foreach (var itemId in equipmentIds)
        {
            if (!string.IsNullOrEmpty(itemId))
            {
                var itemData = ItemDatabase.Instance.GetItemDataById(itemId);
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

    #region Equipment Visual Updates

    /// <summary>
    /// Inicializa los listeners de eventos de equipamiento.
    /// </summary>
    private void InitializeEventListeners()
    {
        if (!_isEventListenerInitialized)
        {
            InventoryManager.OnItemEquipped += OnItemEquipped;
            InventoryManager.OnItemUnequipped += OnItemUnequipped;
            _isEventListenerInitialized = true;
            Debug.Log("[HeroVisualManagementSystem] Event listeners initialized");
        }
    }

    /// <summary>
    /// Desuscribe de los eventos de inventario.
    /// </summary>
    private void UnsubscribeFromEvents()
    {
        if (_isEventListenerInitialized)
        {
            InventoryManager.OnItemEquipped -= OnItemEquipped;
            InventoryManager.OnItemUnequipped -= OnItemUnequipped;
            _isEventListenerInitialized = false;
            Debug.Log("[HeroVisualManagementSystem] Event listeners unsubscribed");
        }
    }

    /// <summary>
    /// Maneja el evento de ítem equipado.
    /// </summary>
    /// <param name="equippedItem">InventoryItem equipado</param>
    /// <param name="unequippedItem">InventoryItem que fue desequipado (puede ser null)</param>
    private void OnItemEquipped(InventoryItem equippedItem, InventoryItem unequippedItem)
    {
        if (equippedItem == null) return;
        Debug.Log($"[HeroVisualManagementSystem] Item equipped: {equippedItem.itemId} (Instance: {equippedItem.instanceId})");
        
        // Si había un ítem equipado previamente, ocultarlo primero
        if (unequippedItem != null)
        {
            UpdateHeroVisualEquipment(unequippedItem.itemId, false);
        }
        
        // Mostrar el nuevo ítem equipado
        UpdateHeroVisualEquipment(equippedItem.itemId, true);
    }

    /// <summary>
    /// Maneja el evento de ítem desequipado.
    /// </summary>
    /// <param name="item">InventoryItem desequipado</param>
    private void OnItemUnequipped(InventoryItem item)
    {
        if (item == null) return;
        Debug.Log($"[HeroVisualManagementSystem] Item unequipped: {item.itemId} (Instance: {item.instanceId})");
        UpdateHeroVisualEquipment(item.itemId, false);
    }

    /// <summary>
    /// Actualiza el visual del héroe cuando se equipa/desequipa un ítem.
    /// </summary>
    /// <param name="itemId">ID del ítem</param>
    /// <param name="isEquipping">True si se está equipando, false si se está desequipando</param>
    private void UpdateHeroVisualEquipment(string itemId, bool isEquipping)
    {
        // Buscar la entidad del héroe local con visual instanciado
        var heroQuery = EntityManager.CreateEntityQuery(
            typeof(HeroVisualInstance), 
            typeof(IsLocalPlayer)
        );
        
        if (heroQuery.IsEmpty) 
        {
            Debug.LogWarning("[HeroVisualManagementSystem] No local hero with visual instance found");
            return;
        }
        
        var heroEntity = heroQuery.GetSingletonEntity();
        var visualInstance = EntityManager.GetComponentData<HeroVisualInstance>(heroEntity);
        
        // Encontrar el GameObject usando el ID
        var visualGameObject = FindGameObjectById(visualInstance.visualInstanceId);
        if (visualGameObject != null)
        {
            ApplyEquipmentChange(visualGameObject, itemId, isEquipping);
        }
        else
        {
            Debug.LogWarning($"[HeroVisualManagementSystem] Visual GameObject not found for instance ID: {visualInstance.visualInstanceId}");
        }
        
        heroQuery.Dispose();
    }

    /// <summary>
    /// Aplica el cambio de equipamiento al GameObject visual.
    /// </summary>
    /// <param name="visualInstance">GameObject visual del héroe</param>
    /// <param name="itemId">ID del ítem</param>
    /// <param name="isEquipping">True si se está equipando</param>
    private void ApplyEquipmentChange(GameObject visualInstance, string itemId, bool isEquipping)
    {
        var heroData = PlayerSessionService.SelectedHero;
        if (heroData == null) 
        {
            Debug.LogWarning("[HeroVisualManagementSystem] No selected hero data available");
            return;
        }
        
        var itemData = ItemDatabase.Instance.GetItemDataById(itemId);
        if (itemData == null || string.IsNullOrEmpty(itemData.visualPartId)) 
        {
            Debug.LogWarning($"[HeroVisualManagementSystem] Item data or visualPartId not found for item: {itemId}");
            return;
        }
        
        var avatarPartDatabase = Resources.Load<Data.Avatar.AvatarPartDatabase>("Data/Avatar/AvatarPartDatabase");
        if (avatarPartDatabase == null) 
        {
            Debug.LogError("[HeroVisualManagementSystem] AvatarPartDatabase not found in Resources");
            return;
        }
        
        var gender = heroData.gender == "Male" ? Gender.Male : Gender.Female;
        
        if (isEquipping)
        {
            // Equipar nueva pieza
            Debug.Log($"[HeroVisualManagementSystem] Equipping visual part: {itemData.visualPartId}");
            Data.Avatar.AvatarVisualUtils.ToggleArmorVisibilityByAvatarPartId(
                visualInstance.transform,
                avatarPartDatabase,
                itemData.visualPartId,
                gender
            );
        }
        else
        {
            // Desequipar pieza - resetear el slot completo
            Debug.Log($"[HeroVisualManagementSystem] Unequipping item type: {itemData.itemType}");
            Data.Avatar.AvatarVisualUtils.UnequipSlotVisual(
                visualInstance.transform,
                avatarPartDatabase,
                itemData.itemType,
                gender,
                heroData
            );
        }
    }

    /// <summary>
    /// Encuentra un GameObject por su instance ID.
    /// </summary>
    /// <param name="instanceId">Instance ID del GameObject</param>
    /// <returns>GameObject encontrado o null</returns>
    private GameObject FindGameObjectById(int instanceId)
    {
        // Buscar GameObject por instance ID
        var allObjects = Object.FindObjectsOfType<GameObject>();
        return System.Array.Find(allObjects, obj => obj.GetInstanceID() == instanceId);
    }

    #endregion
}
