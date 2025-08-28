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
    [SerializeField] private BaseItemCellController itemCellController;
    [SerializeField] private TMP_Text productNameText;
    [SerializeField] private Image currencyIcon;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private Button buyButton;

    private InventoryItem _productData;
    private ItemData _protoProduct;
    private System.Action<InventoryItem, ItemData> _onBuy;

    public void Setup(ItemData itemData, System.Action<InventoryItem, ItemData> onBuy)
    {
        _protoProduct = itemData;
        _onBuy = onBuy;
        InventoryItem inventoryItem = ItemInstanceService.CreateItem(itemData.id);
        _productData = inventoryItem;
        itemCellController.SetItem(inventoryItem, itemData);
        productNameText.text = itemData.name;
        costText.text = "300";
        // Cargar icono de moneda si es necesario
        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(OnBuyClicked);
    }

    private void OnBuyClicked()
    {
        _onBuy?.Invoke(_productData, _protoProduct);
    }
}
