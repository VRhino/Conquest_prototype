using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Data.Items;

/// <summary>
/// Controlador para el widget ItemSelector.
/// Maneja la paginación de una lista de items en 10 celdas fijas.
/// Recibe items desde fuera y permite navegación con chevrons.
/// </summary>
public class ItemSelectorController : MonoBehaviour
{
    #region UI References

    [Header("Navigation")]
    [SerializeField] private Button rightChevron;
    [SerializeField] private Button leftChevron;

    [Header("Container")]
    [SerializeField] private Transform itemContainer;
    [SerializeField] private GameObject itemCellPrefab;
    [SerializeField] private TooltipManager tooltipManager;

    #endregion

    #region Private Fields

    private List<ItemCellController> _itemCells = new List<ItemCellController>();
    private List<InventoryItem> _allItems = new List<InventoryItem>();
    private int _currentPageIndex = 0;
    private int _totalPages = 0;
    private bool _isInitialized = false;
    private const int ITEMS_PER_PAGE = 10;

    // Callbacks asignados externamente
    private Action<InventoryItem, ItemDataSO> _onItemClicked;

    #endregion

    #region Events

    /// <summary>
    /// Se dispara cuando se hace click en un placeholder.
    /// </summary>
    public Action OnPlaceholderClicked;

    #endregion

    #region Unity Lifecycle

    void OnDestroy()
    {
        ClearButtonListeners();
    }

    #endregion

    #region Getters

    public bool IsInitialized => _isInitialized;
    public bool IsEnabled => gameObject.activeSelf;

    #endregion

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void ShowNearElement(RectTransform targetElement)
    {
        if (targetElement == null) return;

        // Posicionar el widget cerca del elemento objetivo
        Vector3[] corners = new Vector3[4];
        targetElement.GetWorldCorners(corners);
        Vector3 targetPosition = corners[2]; // Esquina superior derecha

        // Ajustar la posición del widget
        RectTransform rectTransform = GetComponent<RectTransform>();
        Vector3 offset = new Vector3(10f, -10f, 0f); // Desplazamiento para evitar solapamiento
        rectTransform.position = targetPosition + offset;

        Show();
    }

    public void Hide()
    {
        if (tooltipManager != null) tooltipManager.HideAllTooltips();
        gameObject.SetActive(false);
    }

    #region Initialization

    /// <summary>
    /// Inicializa el widget. Debe ser llamado externamente antes de usar.
    /// </summary>
    public void Initialize()
    {
        if (_isInitialized) return;

        // Instanciar las 10 celdas
        for (int i = 0; i < ITEMS_PER_PAGE; i++)
        {
            GameObject cellObj = Instantiate(itemCellPrefab, itemContainer);
            ItemCellController cell = cellObj.GetComponent<ItemCellController>();
            if (cell != null)
            {
                cell.SetItem(null, null);
                ConnectWithTooltipsEvents(cell);
                _itemCells.Add(cell);
            }
        }

        // Configurar listeners de botones
        SetupButtonListeners();

        _isInitialized = true;
    }

    public void SetTooltipManager(TooltipManager manager)
    {
        tooltipManager = manager;
    }
    private void ConnectWithTooltipsEvents(ItemCellController cell)
    {
        if (tooltipManager == null || cell == null) return;
        tooltipManager.ConnectCellToTooltip(cell);
    }

    private void disconnectWithTooltipEvents(ItemCellController cell)
    {
        if (tooltipManager == null || cell == null) return;
        tooltipManager.DisconnectCellFromTooltip(cell);
    }

    public void ClearAll()
    {
        if (!_isInitialized) return;

        ClearButtonListeners();
        _itemCells.Clear();
        foreach (Transform child in itemContainer)
        {
            ItemCellController cell = child.GetComponent<ItemCellController>();
            if (cell != null) disconnectWithTooltipEvents(cell);
            Destroy(child.gameObject);
        }
        _allItems.Clear();
        _currentPageIndex = 0;
        _totalPages = 0;
        _isInitialized = false;
    }

    /// <summary>
    /// Configura los callbacks para eventos de las celdas.
    /// Debe ser llamado después de Initialize().
    /// </summary>
    public void SetEvents(Action<InventoryItem, ItemDataSO> onItemClicked)
    {
        _onItemClicked = onItemClicked;
        foreach (ItemCellController cell in _itemCells)
        {
            cell.SetEvents(_onItemClicked, null);
        }
    }

    #endregion

    #region Button Listeners

    private void SetupButtonListeners()
    {
        if (rightChevron != null) rightChevron.onClick.AddListener(GoToNextPage);
        if (leftChevron != null) leftChevron.onClick.AddListener(GoToPreviousPage);
    }

    private void ClearButtonListeners()
    {
        if (rightChevron != null) rightChevron.onClick.RemoveListener(GoToNextPage);
        if (leftChevron != null) leftChevron.onClick.RemoveListener(GoToPreviousPage);
    }

    #endregion

    #region Pagination

    private void RecalculatePagination()
    {
        _totalPages = Mathf.CeilToInt((float)_allItems.Count / ITEMS_PER_PAGE);
        UpdateChevronVisibility();
    }

    private void UpdateChevronVisibility()
    {
        if (leftChevron != null) leftChevron.gameObject.SetActive(_currentPageIndex > 0);
        if (rightChevron != null) rightChevron.gameObject.SetActive(_currentPageIndex < _totalPages - 1);
    }

    private void GoToNextPage()
    {
        if (_currentPageIndex < _totalPages - 1)
        {
            _currentPageIndex++;
            RenderCurrentPage();
        }
    }

    private void GoToPreviousPage()
    {
        if (_currentPageIndex > 0)
        {
            _currentPageIndex--;
            RenderCurrentPage();
        }
    }

    #endregion

    #region Item Management

    /// <summary>
    /// Asigna una nueva lista de items al widget.
    /// Vacía las celdas existentes y muestra los nuevos items.
    /// </summary>
    public void SetItems(List<InventoryItem> items)
    {
        if (!_isInitialized)
        {
            Debug.LogError("[ItemSelectorController] Not initialized. Call Initialize() first.");
            return;
        }

        _allItems = items ?? new List<InventoryItem>();
        _currentPageIndex = 0;

        // Vaciar todas las celdas
        foreach (var cell in _itemCells)
        {
            cell.SetItem(null, null);
        }

        RecalculatePagination();
        RenderCurrentPage();
    }

    private void RenderCurrentPage()
    {
        int startIndex = _currentPageIndex * ITEMS_PER_PAGE;
        for (int i = 0; i < _itemCells.Count; i++)
        {
            int itemIndex = startIndex + i;
            if (itemIndex < _allItems.Count)
            {
                InventoryItem item = _allItems[itemIndex];
                ItemDataSO itemData = ItemService.GetItemById(item.itemId);
                _itemCells[i].SetItem(item, itemData);
            }
            else
            {
                _itemCells[i].SetItem(null, null);
            }
        }
    }

    #endregion
}
