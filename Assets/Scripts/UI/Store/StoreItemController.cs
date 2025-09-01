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

    /// <summary>
    /// Obtiene los datos del producto para validaciones externas.
    /// </summary>
    public ItemData ProtoProduct => _protoProduct;

    /// <summary>
    /// Obtiene la instancia específica del ítem que se muestra en la tienda.
    /// Esta es la misma instancia que ve el usuario en los tooltips.
    /// </summary>
    public InventoryItem GetProductInstance() => _productData;

    public void Setup(ItemData itemData, System.Action<InventoryItem, ItemData> onBuy, InventoryTooltipManager tooltipManager)
    {
        _protoProduct = itemData;
        _onBuy = onBuy;
        InventoryItem inventoryItem = ItemInstanceService.CreateItem(itemData.id);
        _productData = inventoryItem;
        _tooltipManager = tooltipManager;
        itemCellController.SetItem(inventoryItem, itemData);
        productNameText.text = itemData.name;
        costText.text = inventoryItem.price.ToString();
        
        // Conectar eventos de tooltip
        ConnectWithTooltipsEvents();
        
        // Configurar botón de compra
        UpdateBuyButtonState();
        
        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(OnBuyClicked);
    }

    /// <summary>
    /// Actualiza el estado del botón de compra basado en si el jugador puede permitirse el item.
    /// </summary>
    private void UpdateBuyButtonState()
    {
        if (buyButton == null || _protoProduct == null) return;

        // Para evitar dependency issues, delegamos la verificación al controller padre
        // Por ahora, solo mantenemos el botón activo
        buyButton.interactable = true;
        
        // TODO: El UIStoreController puede llamar a UpdateBuyButtonAvailability después de transacciones
    }

    /// <summary>
    /// Método público para que el UIStoreController actualice la disponibilidad del botón.
    /// </summary>
    /// <param name="canAfford">True si el jugador puede permitirse este item</param>
    public void UpdateBuyButtonAvailability(bool canAfford)
    {
        if (buyButton == null) return;

        buyButton.interactable = canAfford;
        
        // Cambiar el color del botón
        var buttonImage = buyButton.GetComponent<Image>();
        if (buttonImage != null)
        {
            buttonImage.color = canAfford ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);
        }
        
        // Cambiar texto del botón
        var buttonText = buyButton.GetComponentInChildren<TMP_Text>();
        if (buttonText != null)
        {
            buttonText.text = canAfford ? "Buy" : "Can't Afford";
        }
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
