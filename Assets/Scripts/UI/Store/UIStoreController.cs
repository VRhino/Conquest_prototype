using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Data.Items;

/// <summary>Store presentation delegates purchases and sales to the transaction service.</summary>
public class UIStoreController : MonoBehaviour, IFullscreenPanel
{
    [Header("UI References")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Transform goodsContainer;
    [SerializeField] private GameObject storeItemPrefab;
    [SerializeField] private Button exitButton;
    [SerializeField] private InventoryPanelController auxiliaryInventoryPanel;
    private TooltipManager tooltipManager;
    private readonly List<StoreItemController> _activeItems = new();
    private Action _onExit;

    private void Awake()
    {
        if (exitButton != null) exitButton.onClick.AddListener(OnExitClicked);
    }
    private void OnEnable() => InventoryManager.OnInventoryChanged += UpdateAllBuyButtonStates;
    private void OnDisable() => InventoryManager.OnInventoryChanged -= UpdateAllBuyButtonStates;
    private void OnDestroy()
    {
        if (exitButton != null) exitButton.onClick.RemoveListener(OnExitClicked);
    }
    private void OnExitClicked() => FullscreenPanelManager.Instance.ClosePanel<UIStoreController>();

    public void Initialize(List<string> productsIds, Action onExit = null)
    {
        _onExit = onExit;
        ClearItems();
        tooltipManager = FindTooltipManager();
        foreach (var productId in productsIds)
        {
            var product = ItemService.GetItemById(productId);
            if (product == null) continue;
            var go = Instantiate(storeItemPrefab, goodsContainer);
            var controller = go.GetComponent<StoreItemController>();
            if (controller == null) { Destroy(go); continue; }
            controller.Setup(product, OnProductPurchased, tooltipManager);
            _activeItems.Add(controller);
        }
        UpdateAllBuyButtonStates();
    }

    public TooltipManager FindTooltipManager() => FindObjectOfType<TooltipManager>();
    public void SetStoreData(StoreData storeData)
    {
        if (storeData == null) return;
        titleText.text = storeData.storeTitle;
        Initialize(storeData.productIds);
    }
    private void ClearItems()
    {
        foreach (var item in _activeItems) if (item != null) Destroy(item.gameObject);
        _activeItems.Clear();
    }

    private void OnProductPurchased(InventoryItem product, ItemDataSO definition)
    {
        var offering = _activeItems.Find(cell => cell != null && ReferenceEquals(cell.GetProductInstance(), product));
        if (offering == null) return;
        var result = StoreTransactionService.PurchaseItem(product);
        if (!result.Success) { ShowTransactionMessage(result.ErrorMessage, false); return; }
        _activeItems.Remove(offering);
        Destroy(offering.gameObject);
        ShowTransactionMessage($"Purchased {definition.name} for {result.RequiredBronze} Bronze", true);
    }

    public void SellItem(InventoryItem item, ItemDataSO definition)
    {
        var result = StoreTransactionService.SellItem(item);
        ShowTransactionMessage(result.Success ? $"Sold {definition.name} for {result.RequiredBronze} Bronze" : result.ErrorMessage, result.Success);
    }

    public bool IsOpen => gameObject.activeSelf;
    public bool IsPanelOpen => IsOpen;
    public void OpenPanel()
    {
        if (!PlayerSessionService.HasHero) return;
        gameObject.SetActive(true);
        auxiliaryInventoryPanel?.OpenAsAuxiliaryPanel(null, SellItem);
        UpdateAllBuyButtonStates();
    }
    public void ClosePanel()
    {
        tooltipManager?.HideAllTooltips();
        auxiliaryInventoryPanel?.CloseAsAuxiliaryPanel();
        gameObject.SetActive(false);
        var onExit = _onExit;
        _onExit = null;
        onExit?.Invoke();
    }
    public void TogglePanel() { if (IsOpen) ClosePanel(); else OpenPanel(); }

    private void UpdateAllBuyButtonStates()
    {
        var hero = PlayerSessionService.SelectedHero;
        foreach (var cell in _activeItems)
            if (cell != null && cell.ProtoProduct != null)
                cell.UpdateBuyButtonAvailability(hero != null && hero.bronze >=
                    (long)ItemPricingService.CalculateItemPrice(cell.ProtoProduct, cell.GetProductInstance()) * cell.GetProductInstance().quantity);
    }
    private void ShowTransactionMessage(string message, bool success)
    {
        if (success) Debug.Log($"[UIStoreController] {message}");
        else Debug.LogWarning($"[UIStoreController] {message}");
    }
}
