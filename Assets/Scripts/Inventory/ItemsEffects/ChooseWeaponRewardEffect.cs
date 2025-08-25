using UnityEngine;
using Data.Items;
using System.Collections.Generic;

/// <summary>
/// ItemEffect that opens a RewardDialog for the player to choose one of several weapons.
/// </summary>
[CreateAssetMenu(fileName = "ChooseWeaponRewardEffect", menuName = "Items/Effects/Choose Weapon Reward", order = 10)]
public class ChooseWeaponRewardEffect : ItemEffect
{
    [Header("Reward Dialog Settings")]
    [SerializeField] private RewardDialogDataSO rewardDialogData;
    [SerializeField] private List<string> weaponItemIds = new List<string>();

    public override bool Execute(HeroData hero, int quantity = 1)
    {
        if (hero == null || rewardDialogData == null || weaponItemIds == null || weaponItemIds.Count < 2)
        {
            Debug.LogError("[ChooseWeaponRewardEffect] Invalid configuration or hero");
            return false;
        }

        // Configure dialog for weapon selection
        rewardDialogData.title = "Choose Your Weapon";
        rewardDialogData.description = "Select one weapon from the available options.";
        rewardDialogData.rewardItemIds = weaponItemIds;
        rewardDialogData.allowSelection = true;
        rewardDialogData.minSelection = 1;
        rewardDialogData.maxSelection = 1;
        rewardDialogData.buttonText = "Accept";

        // Find or create the RewardDialogUIController in the scene
        var dialogController = Object.FindObjectOfType<RewardDialogUIController>(true);
        if (dialogController == null)
        {
            Debug.LogError("[ChooseWeaponRewardEffect] No RewardDialogUIController found in scene");
            return false;
        }

        dialogController.Open(rewardDialogData, hero);
        return true;
    }

    public override string GetPreviewText(int quantity = 1)
    {
        return "Choose one weapon from the available options.";
    }

    public override bool CanExecute(HeroData hero)
    {
        return hero != null && weaponItemIds != null && weaponItemIds.Count >= 2;
    }
}
