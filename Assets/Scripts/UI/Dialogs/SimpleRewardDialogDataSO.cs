using System.Collections.Generic;
using UnityEngine;
using Data.Items;
// using Services; // Uncomment if HeroInventoryService is in a Services namespace

/// <summary>
/// Concrete implementation of RewardDialogDataSO for simple item delivery.
/// Delivers all selected items to the hero's inventory.
/// </summary>
[CreateAssetMenu(fileName = "SimpleRewardDialogDataSO", menuName = "UI/Dialogs/Simple Reward Dialog Data", order = 2)]
public class SimpleRewardDialogDataSO : RewardDialogDataSO
{
    /// <summary>
    /// Delivers the selected items to the hero's inventory.
    /// </summary>
    /// <param name="selectedItems">The list of selected itemIds</param>
    /// <param name="hero">The hero receiving the rewards</param>
    public override void DeliverRewards(List<string> rewardItemIds, HeroData hero)
    {
        if (hero == null || rewardItemIds == null || rewardItemIds.Count == 0)
            return;
        
        foreach (var itemId in rewardItemIds)
            {
                var itemData = ItemDatabase.Instance.GetItemDataById(itemId);
                if (itemData != null)
                {
                    InventoryManager.CreateAndAddItem(itemId, 1);
                }
                else
                {
                    Debug.LogWarning($"[SimpleRewardDialogDataSO] Item with ID '{itemId}' not found in database");
                }
            }
    }
}
