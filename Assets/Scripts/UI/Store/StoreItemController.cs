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

    private StoreProductData _productData;
    private System.Action<StoreProductData> _onBuy;

    public void Setup(StoreProductData productData, System.Action<StoreProductData> onBuy)
    {
        _productData = productData;
        _onBuy = onBuy;
        var itemData = ItemDatabase.Instance.GetItemDataById(productData.itemId);
        itemCellController.SetPreviewItem(itemData);
        productNameText.text = productData.productName;
        costText.text = productData.cost.ToString();
        // Cargar icono de moneda si es necesario
        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(OnBuyClicked);
    }

    private void OnBuyClicked()
    {
        _onBuy?.Invoke(_productData);
    }
}
