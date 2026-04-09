using Unity.Entities;
using UnityEngine;
using Data.Items;

/// <summary>
/// Escucha eventos de InventoryManager (equip/unequip) y actualiza el visual
/// del héroe local en tiempo real. Solo actúa sobre el jugador local.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class HeroVisualEquipmentSystem : SystemBase
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

    protected override void OnUpdate() { }

    private void InitializeEventListeners()
    {
        if (!_isEventListenerInitialized)
        {
            InventoryManager.OnItemEquipped += OnItemEquipped;
            InventoryManager.OnItemUnequipped += OnItemUnequipped;
            _isEventListenerInitialized = true;
            Debug.Log("[HeroVisualEquipmentSystem] Event listeners initialized");
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (_isEventListenerInitialized)
        {
            InventoryManager.OnItemEquipped -= OnItemEquipped;
            InventoryManager.OnItemUnequipped -= OnItemUnequipped;
            _isEventListenerInitialized = false;
            Debug.Log("[HeroVisualEquipmentSystem] Event listeners unsubscribed");
        }
    }

    private void OnItemEquipped(InventoryItem equippedItem, InventoryItem unequippedItem)
    {
        if (equippedItem == null) return;
        Debug.Log($"[HeroVisualEquipmentSystem] Item equipped: {equippedItem.itemId} (Instance: {equippedItem.instanceId})");

        if (unequippedItem != null)
            UpdateHeroVisualEquipment(unequippedItem.itemId, false);

        UpdateHeroVisualEquipment(equippedItem.itemId, true);
    }

    private void OnItemUnequipped(InventoryItem item)
    {
        if (item == null) return;
        Debug.Log($"[HeroVisualEquipmentSystem] Item unequipped: {item.itemId} (Instance: {item.instanceId})");
        UpdateHeroVisualEquipment(item.itemId, false);
    }

    private void UpdateHeroVisualEquipment(string itemId, bool isEquipping)
    {
        var heroQuery = EntityManager.CreateEntityQuery(typeof(HeroVisualInstance), typeof(IsLocalPlayer));

        if (heroQuery.IsEmpty)
        {
            Debug.LogWarning("[HeroVisualEquipmentSystem] No local hero with visual instance found");
            heroQuery.Dispose();
            return;
        }

        var heroEntity = heroQuery.GetSingletonEntity();
        var visualInstance = EntityManager.GetComponentData<HeroVisualInstance>(heroEntity);
        heroQuery.Dispose();

        var visualGameObject = FindGameObjectById(visualInstance.visualInstanceId);
        if (visualGameObject == null)
        {
            Debug.LogWarning($"[HeroVisualEquipmentSystem] Visual GameObject not found for instance ID: {visualInstance.visualInstanceId}");
            return;
        }

        ApplyEquipmentChange(visualGameObject, itemId, isEquipping);
    }

    private static void ApplyEquipmentChange(GameObject visualInstance, string itemId, bool isEquipping)
    {
        var heroData = PlayerSessionService.SelectedHero;
        if (heroData == null)
        {
            Debug.LogWarning("[HeroVisualEquipmentSystem] No selected hero data available");
            return;
        }

        var itemData = ItemService.GetItemById(itemId);
        if (itemData == null || string.IsNullOrEmpty(itemData.visualPartId))
        {
            Debug.LogWarning($"[HeroVisualEquipmentSystem] Item data or visualPartId not found for item: {itemId}");
            return;
        }

        var avatarPartDatabase = Resources.Load<Data.Avatar.AvatarPartDatabase>("Data/Avatar/AvatarPartDatabase");
        if (avatarPartDatabase == null)
        {
            Debug.LogError("[HeroVisualEquipmentSystem] AvatarPartDatabase not found in Resources");
            return;
        }

        var gender = heroData.gender == "Male" ? Gender.Male : Gender.Female;

        if (isEquipping)
        {
            Debug.Log($"[HeroVisualEquipmentSystem] Equipping visual part: {itemData.visualPartId}");
            Data.Avatar.AvatarVisualUtils.ToggleArmorVisibilityByAvatarPartId(
                visualInstance.transform, avatarPartDatabase, itemData.visualPartId, gender);
        }
        else
        {
            Debug.Log($"[HeroVisualEquipmentSystem] Unequipping item type: {itemData.itemType}");
            Data.Avatar.AvatarVisualUtils.UnequipSlotVisual(
                visualInstance.transform, avatarPartDatabase,
                itemData.itemType, itemData.itemCategory, gender, heroData);
        }
    }

    private static GameObject FindGameObjectById(int instanceId)
    {
        var allObjects = Object.FindObjectsOfType<GameObject>();
        return System.Array.Find(allObjects, obj => obj.GetInstanceID() == instanceId);
    }
}
