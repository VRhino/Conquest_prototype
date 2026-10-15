using System.Collections.Generic;
using UnityEngine;
using Data.Items;

[CreateAssetMenu(fileName = "SimpleRewardDialogDataSO", menuName = "UI/Dialogs/Simple Reward Dialog Data", order = 2)]
public class SimpleRewardDialogDataSO : RewardDialogDataSO
{
    public override void DeliverRewards(List<string> ids, HeroData hero) => TryDeliverRewards(ids, hero);
    public override bool TryDeliverRewards(List<string> ids, HeroData hero)
    {
        if (hero == null || !ReferenceEquals(hero, InventoryManager.GetCurrentHero()) || ids == null || ids.Count == 0) return false;
        return InventoryEventService.RunTransaction(() =>
        {
            foreach (var id in ids) if (!InventoryManager.CreateAndAddItem(id)) return false;
            return true;
        });
    }
}
