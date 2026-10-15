using UnityEngine;
using Data.Items;
using System;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "ChooseWeaponRewardEffect", menuName = "Items/Effects/Choose Weapon Reward", order = 10)]
public class ChooseWeaponRewardEffect : ItemEffect
{
    [SerializeField] private RewardDialogDataSO rewardDialogData;
    [SerializeField] private List<string> weaponItemIds = new();
    public override bool RequiresSelection => true;
    public override bool Execute(HeroData hero, int quantity = 1)
        => quantity == 1 && BeginSelection(hero, ids => ExecuteSelection(hero, ids), null);
    public override bool BeginSelection(HeroData hero, Func<List<string>, bool> selected, Action cancelled)
    {
        if (!CanExecute(hero)) return false;
        var dialog = UnityEngine.Object.FindObjectOfType<RewardDialogUIController>(true);
        if (dialog == null || dialog.IsOpen) return false;
        var data = Instantiate(rewardDialogData);
        data.title = "Elige tu arma";
        data.description = "Selecciona un arma para recibirla.";
        data.rewardItemIds = new List<string>(weaponItemIds);
        data.allowSelection = true;
        data.minSelection = data.maxSelection = 1;
        data.buttonText = "Aceptar";
        dialog.Open(data, hero, selected, cancelled, true);
        return true;
    }
    public override bool ExecuteSelection(HeroData hero, List<string> ids)
        => ids != null && ids.Count == 1 && weaponItemIds.Contains(ids[0]) && rewardDialogData.TryDeliverRewards(ids, hero);
    public override string GetPreviewText(int quantity = 1) => "Elige un arma de las opciones disponibles.";
    public override bool CanExecute(HeroData hero)
        => hero != null && rewardDialogData != null && weaponItemIds != null && weaponItemIds.Count > 1;
}
