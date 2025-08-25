using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Data.Items;

/// <summary>
/// UI Controller for the Reward Dialog.
/// Handles displaying reward items, selection logic, and delegates reward delivery to the assigned RewardDialogDataSO.
/// </summary>
public class RewardDialogUIController : MonoBehaviour
{
    [Header("Dialog Data")]
    public RewardDialogDataSO dialogData;

    [Header("UI References")]
    public GameObject mainPanel;
    public TMP_Text titleText;
    public TMP_Text descriptionText;
    public Transform rewardItemsContainer;
    public GameObject inventoryItemCellPrefab;
    public Button acceptButton;

    private List<string> selectedItemIds = new List<string>();
    private HeroData _currentHero;

    public void Open(RewardDialogDataSO data, HeroData hero)
    {
        dialogData = data;
        _currentHero = hero;
        mainPanel.SetActive(true);
        SetupUI();
        acceptButton.onClick.AddListener(OnAcceptButtonPressed);
    }

    private void SetupUI()
    {
        titleText.text = dialogData.title;
        descriptionText.text = dialogData.description;
        acceptButton.GetComponentInChildren<TMP_Text>().text = dialogData.buttonText;
        acceptButton.interactable = !dialogData.allowSelection || dialogData.minSelection <= 0;
        PopulateRewardItems();
    }

    private void PopulateRewardItems()
    {
        foreach (Transform child in rewardItemsContainer)
            Destroy(child.gameObject);

        selectedItemIds.Clear();
        foreach (var itemId in dialogData.rewardItemIds)
        {
            var itemData = ItemDatabase.Instance.GetItemDataById(itemId);
            var cellGO = Instantiate(inventoryItemCellPrefab, rewardItemsContainer);
            cellGO.transform.localScale = Vector3.one * 1.375f;
            var cellController = cellGO.GetComponent<InventoryItemCellController>();
            cellController.SetPreviewItem(itemData); // Only show proto info

            if (dialogData.allowSelection)
            {
                Image selectedOverlay = cellController.selectedOverlay;
                var button = cellGO.GetComponent<Button>();
                if (selectedOverlay != null)
                {
                    button.onClick.AddListener(() => OnItemCellClicked(itemId, button, selectedOverlay));
                }
                else
                {
                    Debug.LogWarning("[RewardDialogUIController] InventoryItemCellPrefab is missing a Button component for selection.");
                }
            }
        }
    }

    private void OnItemCellClicked(string itemId, Button button, Image selectedOverlay)
    {
        if (selectedItemIds.Contains(itemId))
        {
            selectedItemIds.Remove(itemId);
            selectedOverlay.gameObject.SetActive(false);
        }
        else
        {
            if (selectedItemIds.Count < dialogData.maxSelection)
            {
                selectedItemIds.Add(itemId);
                selectedOverlay.gameObject.SetActive(true);
            }
        }
        acceptButton.interactable = selectedItemIds.Count >= dialogData.minSelection;
    }

    public void OnAcceptButtonPressed()
    {
        List<string> rewards = new List<string>();
        if (dialogData.allowSelection) rewards.AddRange(selectedItemIds);
        else rewards.AddRange(dialogData.rewardItemIds);

        dialogData.DeliverRewards(rewards, _currentHero);
        Close();
    }

    public void Close()
    {
        mainPanel.SetActive(false);
        dialogData = null;
        _currentHero = null;
        selectedItemIds.Clear();
    }
}
