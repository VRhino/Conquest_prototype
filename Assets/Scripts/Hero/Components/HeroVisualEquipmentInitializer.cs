using System.Collections.Generic;
using System.Linq;
using ConquestTactics.Visual;
using Data.Avatar;
using UnityEngine;

/// <summary>Updates the linked local visual after a completed inventory change.</summary>
public class HeroVisualEquipmentInitializer : MonoBehaviour
{
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private AvatarPartDatabase avatarPartDatabase;
    readonly List<InventoryItem> visibleEquipment = new();
    EntityVisualSync sync;
    HeroData boundHero;

    public void Bind(EntityVisualSync visualSync)
    {
        sync = visualSync;
        boundHero = PlayerSessionService.SelectedHero;
        visibleEquipment.Clear();
        if (boundHero != null && sync.IsLocalHero)
            visibleEquipment.AddRange(EquipmentManagerService.GetAllEquippedItems());
    }

    void OnEnable() => InventoryManager.OnInventoryChanged += HandleInventoryChange;
    void OnDisable() => InventoryManager.OnInventoryChanged -= HandleInventoryChange;
    void HandleInventoryChange()
    {
        if (!visibleEquipment.SequenceEqual(EquipmentManagerService.GetAllEquippedItems())) ForceRefreshHeroVisual();
    }

    [ContextMenu("Force Refresh Hero Visual")]
    public void ForceRefreshHeroVisual()
    {
        if (sync == null || !sync.IsLocalHero || boundHero == null
            || !ReferenceEquals(boundHero, PlayerSessionService.SelectedHero)) return;
        if (avatarPartDatabase == null)
            avatarPartDatabase = Resources.Load<AvatarPartDatabase>("Data/Avatar/AvatarPartDatabase");
        if (avatarPartDatabase == null) return;
        var equipped = EquipmentManagerService.GetAllEquippedItems();
        var gender = boundHero.gender == "Male" ? Gender.Male : Gender.Female;
        foreach (var previous in visibleEquipment)
        {
            if (previous == null || equipped.Contains(previous)) continue;
            var data = InventoryUtils.GetItemData(previous.itemId);
            if (data != null)
                AvatarVisualUtils.UnequipSlotVisual(transform, avatarPartDatabase,
                    data.itemType, data.itemCategory, gender, boundHero);
        }
        // Apply final pieces after restoring bases, including overlapping attachments.
        foreach (var item in equipped)
        {
            if (item == null) continue;
            var data = InventoryUtils.GetItemData(item.itemId);
            if (data != null && !string.IsNullOrEmpty(data.visualPartId))
                AvatarVisualUtils.ToggleArmorVisibilityByAvatarPartId(transform, avatarPartDatabase, data.visualPartId, gender);
        }
        visibleEquipment.Clear();
        visibleEquipment.AddRange(equipped);
        if (enableDebugLogs) Debug.Log("[HeroVisualEquipmentInitializer] Local equipment refreshed");
    }
}
