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
    [SerializeField] private InventoryPanelController auxiliaryInventoryPanel;

    private TooltipManager tooltipManager;

    private List<StoreItemController> _activeItems = new();
    private System.Action _onExit;

    void Start()
    {
        // Los servicios de transacciones se manejan de forma inline en cada operación
    }

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
        
        // Actualizar disponibilidad de todos los botones después de setup
        UpdateAllBuyButtonStates();
        
        if (exitButton != null)
            exitButton.onClick.AddListener(() => FullscreenPanelManager.Instance.ClosePanel<UIStoreController>());
    }

    public TooltipManager FindTooltipManager()
    {
        TooltipManager _tooltipManager = FindObjectOfType<TooltipManager>();
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
        // Verificar que tenemos un héroe seleccionado
        var currentHero = PlayerSessionService.SelectedHero;
        if (currentHero == null)
        {
            ShowTransactionMessage("No hero selected", false);
            return;
        }

        // Calcular precio
        int itemPrice = ItemPricingService.CalculateItemPrice(protoProduct);
        
        // Verificar fondos suficientes
        if (currentHero.bronze < itemPrice)
        {
            ShowTransactionMessage($"Insufficient Bronze. Need {itemPrice}, have {currentHero.bronze}", false);
            return;
        }

        // Verificar espacio en inventario
        if (!InventoryManager.HasSpace())
        {
            ShowTransactionMessage("Not enough space in inventory", false);
            return;
        }

        // Ejecutar transacción
        try
        {
            // Debitar Bronze
            currentHero.bronze -= itemPrice;
            
            // Transferir la instancia específica que el usuario vio en la tienda
            // En lugar de crear una nueva instancia, usamos la misma que se muestra en tooltips
            bool itemAdded = InventoryManager.AddExistingItem(product);
            
            if (itemAdded)
            {
                Debug.Log($"[UIStoreController] Purchase successful: {protoProduct.name}");
                ShowTransactionMessage($"Purchased {protoProduct.name} for {itemPrice} Bronze", true);
                
                // Remover el item de la tienda después de la venta exitosa
                RemoveStoreItem(product, protoProduct);
                
                // Actualizar UI
                UpdateAllBuyButtonStates();
                RefreshAuxiliaryInventory();
                
                // Guardar cambios
                SaveSystem.SavePlayer(PlayerSessionService.CurrentPlayer);
            }
            else
            {
                // Rollback Bronze si falló agregar ítem
                currentHero.bronze += itemPrice;
                ShowTransactionMessage("Failed to add item to inventory", false);
            }
        }
        catch (System.Exception ex)
        {
            ShowTransactionMessage($"Transaction failed: {ex.Message}", false);
        }
    }

    public bool IsOpen => gameObject.activeSelf;

    public bool IsPanelOpen => IsOpen;

    public void ClosePanel()
    {
        gameObject.SetActive(false);
        
        tooltipManager?.HideAllTooltips();
        auxiliaryInventoryPanel?.CloseAsAuxiliaryPanel();
    }

    public void OpenPanel()
    {
        auxiliaryInventoryPanel?.OpenAsAuxiliaryPanel(null, SellItem);
        gameObject.SetActive(true);
    }

    public void SellItem(InventoryItem item, ItemData itemData)
    {
        if (item == null || itemData == null)
        {
            Debug.LogWarning("[UIStoreController] Cannot sell: null item or itemData");
            return;
        }

        // Verificar que tenemos un héroe seleccionado
        var currentHero = PlayerSessionService.SelectedHero;
        if (currentHero == null)
        {
            ShowTransactionMessage("No hero selected", false);
            return;
        }

        // Calcular precio de venta (50% del precio de compra)
        int sellPrice = ItemPricingService.CalculateItemPrice(itemData) / 2;
        
        // Ejecutar transacción de venta
        try
        {
            // Remover ítem del inventario
            bool itemRemoved = InventoryManager.RemoveSpecificItem(item);
            
            if (itemRemoved)
            {
                // Acreditar Bronze
                currentHero.bronze += sellPrice;
                
                Debug.Log($"[UIStoreController] Sale successful: {itemData.name}");
                ShowTransactionMessage($"Sold {itemData.name} for {sellPrice} Bronze", true);
                
                // Actualizar UI
                UpdateAllBuyButtonStates();
                RefreshAuxiliaryInventory();
                
                // Guardar cambios
                SaveSystem.SavePlayer(PlayerSessionService.CurrentPlayer);
            }
            else
            {
                ShowTransactionMessage("Failed to remove item from inventory", false);
            }
        }
        catch (System.Exception ex)
        {
            ShowTransactionMessage($"Sale failed: {ex.Message}", false);
        }
    }

    /// <summary>
    /// Remueve un ítem de la tienda después de ser vendido.
    /// Para equipment (items únicos), el item desaparece completamente.
    /// Para stackables, se podría implementar lógica de stock en el futuro.
    /// </summary>
    /// <param name="soldItem">Item que fue vendido</param>
    /// <param name="protoProduct">Datos del prototipo del item</param>
    private void RemoveStoreItem(InventoryItem soldItem, ItemData protoProduct)
    {
        // Buscar el controlador que maneja este item
        StoreItemController itemToRemove = null;
        
        foreach (var storeItem in _activeItems)
        {
            if (storeItem != null && storeItem.GetProductInstance() == soldItem)
            {
                itemToRemove = storeItem;
                break;
            }
        }
        
        if (itemToRemove != null)
        {
            // Remover de la lista
            _activeItems.Remove(itemToRemove);
            
            // Destruir el GameObject
            if (itemToRemove.gameObject != null)
            {
                Destroy(itemToRemove.gameObject);
            }
            
            Debug.Log($"[UIStoreController] Removed sold item from store: {protoProduct.name}");
        }
    }

    /// <summary>
    /// Refresca el inventario auxiliar después de transacciones.
    /// </summary>
    private void RefreshAuxiliaryInventory()
    {
        if (auxiliaryInventoryPanel != null && auxiliaryInventoryPanel.IsPanelOpen)
        {
            // Forzar actualización del inventario auxiliar
            var currentHero = PlayerSessionService.SelectedHero;
            if (currentHero != null)
            {
                // Usar OpenAsAuxiliaryPanel para refrescar completamente
                auxiliaryInventoryPanel.OpenAsAuxiliaryPanel(null, SellItem);
            }
        }
    }

    /// <summary>
    /// Actualiza el estado de disponibilidad de todos los botones de compra.
    /// </summary>
    private void UpdateAllBuyButtonStates()
    {
        // Verificar que tenemos un héroe seleccionado
        var currentHero = PlayerSessionService.SelectedHero;
        if (currentHero == null)
            return;

        foreach (var storeItem in _activeItems)
        {
            if (storeItem != null && storeItem.ProtoProduct != null)
            {
                // Calcular precio y verificar si se puede permitir
                int itemPrice = ItemPricingService.CalculateItemPrice(storeItem.ProtoProduct);
                bool canAfford = currentHero.bronze >= itemPrice;
                
                storeItem.UpdateBuyButtonAvailability(canAfford);
            }
        }
    }

    /// <summary>
    /// Muestra un mensaje temporal de transacción al usuario.
    /// </summary>
    /// <param name="message">Mensaje a mostrar</param>
    /// <param name="isSuccess">True si es un mensaje de éxito, false si es de error</param>
    private void ShowTransactionMessage(string message, bool isSuccess)
    {
        // Por ahora solo log, pero se puede expandir para mostrar UI toast/popup
        if (isSuccess)
        {
            Debug.Log($"[UIStoreController] {message}");
        }
        else
        {
            Debug.LogWarning($"[UIStoreController] {message}");
        }
        
        // TODO: Implementar UI visual para mostrar mensajes de transacción
        // Ejemplo: mostrar un popup temporal, cambiar color de texto, etc.
    }

    public void TogglePanel()
    {
        if (IsOpen)
            ClosePanel();
        else
            OpenPanel();
    }
}