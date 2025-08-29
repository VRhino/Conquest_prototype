using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Data.Items;

/// <summary>
/// Controlador principal del panel de tienda. Implementa IFullscreenPanel para gestión centralizada.
/// Muestra lista de productos y maneja la compra de ítems.
/// </summary>
public class UIStoreController : MonoBehaviour, IFullscreenPanel
{
    [Header("UI References")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Transform goodsContainer;
    [SerializeField] private GameObject storeItemPrefab;
    [SerializeField] private Button exitButton;

    private InventoryTooltipManager tooltipManager;

    private List<StoreItemController> _activeItems = new();
    private System.Action _onExit;

    public void Initialize(List<string> productsIds, System.Action onExit = null)
    {
        _onExit = onExit;
        ClearItems();
        foreach (var productId in productsIds)
        {
            var go = Instantiate(storeItemPrefab, goodsContainer);
            var controller = go.GetComponent<StoreItemController>();
            var product = ItemDatabase.Instance.GetItemDataById(productId);
            if (product == null)
            {
                Debug.LogWarning($"[UIStoreController] Product with ID '{productId}' not found in ItemDatabase.");
                continue;
            }
            tooltipManager = FindTooltipManager();
            tooltipManager.SetEnableComparisonTooltips(true);
            controller.Setup(product, OnProductPurchased, tooltipManager);
            _activeItems.Add(controller);
        }
        if (exitButton != null)
            exitButton.onClick.AddListener(() => FullscreenPanelManager.Instance.ClosePanel<UIStoreController>());
    }

    public InventoryTooltipManager FindTooltipManager()
    {
        InventoryTooltipManager _tooltipManager = FindObjectOfType<InventoryTooltipManager>();
        if (_tooltipManager == null)
            Debug.LogWarning("[UIStoreController] No InventoryTooltipManager found in the scene.");
        return _tooltipManager;
    }

    public void SetStoreData(StoreData storeData)
    {
        titleText.text = storeData.storeTitle;
        Initialize(storeData.productIds);
    }

    private void ClearItems()
    {
        foreach (var item in _activeItems)
            if (item != null) Destroy(item.gameObject);
        _activeItems.Clear();
    }

    private void OnProductPurchased(InventoryItem product, ItemData protoProduct)
    {
        // Integrar con InventoryManager y lógica de compra;
        // Opcional: feedback visual, actualizar UI, etc.
        Debug.Log($"Purchased product: {protoProduct.name}");
    }

    public bool IsOpen => gameObject.activeSelf;

    public bool IsPanelOpen => IsOpen;

    public void ClosePanel()
    {
        gameObject.SetActive(false);
        tooltipManager?.HideAllTooltips();
    }

    public void OpenPanel()
    {
        gameObject.SetActive(true);
    }

    public void TogglePanel()
    {
        if (IsOpen)
            ClosePanel();
        else
            OpenPanel();
    }
}