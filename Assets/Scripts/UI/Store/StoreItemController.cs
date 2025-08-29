using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Data.Items;

/// <summary>
/// Controlador para cada producto de la tienda. Muestra el ítem, nombre, precio y botón de compra.
/// Reutiliza BaseItemCellController para mostrar el ítem.
/// </summary>
public class StoreItemController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private ItemCellController itemCellController;
    [SerializeField] private TMP_Text productNameText;
    [SerializeField] private Image currencyIcon;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private Button buyButton;
    private InventoryTooltipManager _tooltipManager;

    private InventoryItem _productData;
    private ItemData _protoProduct;
    private System.Action<InventoryItem, ItemData> _onBuy;

    public void Setup(ItemData itemData, System.Action<InventoryItem, ItemData> onBuy, InventoryTooltipManager tooltipManager)
    {
        _protoProduct = itemData;
        _onBuy = onBuy;
        InventoryItem inventoryItem = ItemInstanceService.CreateItem(itemData.id);
        _productData = inventoryItem;
        _tooltipManager = tooltipManager;
        itemCellController.SetItem(inventoryItem, itemData);
        productNameText.text = itemData.name;
        costText.text = "300";
        // Conectar eventos de tooltip
        ConnectWithTooltipsEvents();
        // Cargar icono de moneda si es necesario
        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(OnBuyClicked);
    }

    public void ConnectWithTooltipsEvents()
    {
        if (_tooltipManager == null) return;
        ItemCellInteraction interaction = itemCellController.GetComponent<ItemCellInteraction>();
        if (interaction == null) return;
        // Conectar eventos de tooltip
        interaction.OnItemHoverEnter += _tooltipManager.OnItemHoverEnter;
        interaction.OnItemHoverExit += _tooltipManager.OnItemHoverExit;
        interaction.OnItemHoverMove += _tooltipManager.OnItemHoverMove;
    }

    public void DisconnectFromTooltipEvents()
    {
        if (_tooltipManager == null) return;

        ItemCellInteraction interaction = itemCellController.GetComponent<ItemCellInteraction>();
        if (interaction == null) return;

        // Desconectar eventos de tooltip
        interaction.OnItemHoverEnter -= _tooltipManager.OnItemHoverEnter;
        interaction.OnItemHoverExit -= _tooltipManager.OnItemHoverExit;
        interaction.OnItemHoverMove -= _tooltipManager.OnItemHoverMove;
    }

    private void OnDestroy()
    {
        DisconnectFromTooltipEvents();
    }
    private void OnBuyClicked()
    {
        _onBuy?.Invoke(_productData, _protoProduct);
    }
}
