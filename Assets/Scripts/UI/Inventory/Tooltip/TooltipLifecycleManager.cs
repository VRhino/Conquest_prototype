using UnityEngine;
using Data.Items;
using System.Collections;

/// <summary>
/// Maneja el ciclo de vida de los tooltips: mostrar, ocultar, timers y estados.
/// Componente interno que gestiona la lógica de aparición y desaparición de tooltips.
/// </summary>
public class TooltipLifecycleManager : ITooltipComponent
{
    private string _cellId;
    private InventoryTooltipController _controller;
    private bool _isShowing = false;
    private float _showTimer = 0f;
    private InventoryItem _currentItem;
    private ItemDataSO _currentItemData;
    private Vector3 _lastMousePosition = Vector3.zero;

    #region In case of dual system

    private RectTransform primaryTooltipRect;
    private RectTransform secondaryTooltipRect;

    #endregion

    #region ITooltipComponent Implementation

    public void Initialize(InventoryTooltipController controller)
    {
        _controller = controller;
        _isShowing = false;
        _showTimer = 0f;
        _currentItem = null;
        _currentItemData = null;
        _cellId = "";
        _lastMousePosition = Vector3.zero;
    }

    public void Cleanup()
    {
        if (_isShowing)
        {
            HideTooltip();
        }
        _controller = null;
        _currentItem = null;
        _currentItemData = null;
        _cellId = "";
    }

    #endregion

    #region Public API

    public string CellId => _cellId;

    /// <summary>
    /// Muestra el tooltip para un ítem específico.
    /// </summary>
    public void ShowTooltip(InventoryItem item, ItemDataSO itemData, string cellId)
    {
        _cellId = cellId;
        Debug.Log($"[TooltipLifecycleManager][ShowTooltip] Showing tooltip instantly for item: {itemData?.name} in cell: {cellId}");

        if (item == null || itemData == null)
        {
            HideTooltip();
            return;
        }

        // Validar que el itemId sea válido
        if (!InventoryUtils.ValidateItemParameters(item.itemId))
        {
            HideTooltip();
            return;
        }

        // Si es el mismo ítem, no hacer nada
        if (_isShowing && _currentItem == item && _currentItemData == itemData)
            return;

        _currentItem = item;
        _currentItemData = itemData;
        _showTimer = _controller.ShowDelay;

        // Si no hay delay, mostrar inmediatamente
        if (_showTimer <= 0f)
        {
            ShowTooltipImmediate();
        }
    }

    /// <summary>
    /// Muestra el tooltip con posición específica.
    /// </summary>
    public void ShowTooltip(InventoryItem item, ItemDataSO itemData, Vector3 mousePosition, string cellId)
    {
        _lastMousePosition = mousePosition;
        ShowTooltip(item, itemData, cellId);
    }

    /// <summary>
    /// Muestra el tooltip inmediatamente sin delay.
    /// </summary>
    public void ShowTooltipInstant(InventoryItem item, ItemDataSO itemData, string cellId)
    {
        _currentItem = item;
        _currentItemData = itemData;
        _showTimer = 0f;
        _cellId = cellId;
        ShowTooltipImmediate();
    }

    /// <summary>
    /// Muestra el tooltip inmediatamente con posición específica.
    /// </summary>
    public void ShowTooltipInstant(InventoryItem item, ItemDataSO itemData, Vector3 mousePosition, string cellId)
    {
        _lastMousePosition = mousePosition;
        ShowTooltipInstant(item, itemData, cellId);
    }

    /// <summary>
    /// Oculta el tooltip.
    /// </summary>
    public void HideTooltip()
    {
        // Detener cualquier corrutina activa
        _controller.StopAllCoroutines();

        if (_controller.TooltipPanel != null)
            _controller.TooltipPanel.SetActive(false);

        _isShowing = false;
        _showTimer = 0f;
        _currentItem = null;
        _currentItemData = null;
        _cellId = "";
    }

    #endregion

    #region State Management

    /// <summary>
    /// Actualiza el timer del tooltip (debe llamarse desde Update).
    /// </summary>
    public void UpdateTimer()
    {
        // Manejar delay de aparición
        if (_showTimer > 0f)
        {
            _showTimer -= Time.deltaTime;
            if (_showTimer <= 0f && _currentItem != null && _currentItemData != null)
            {
                ShowTooltipImmediate();
            }
        }
    }

    /// <summary>
    /// Verifica si actualmente se está mostrando un ítem específico.
    /// </summary>
    public bool IsShowingItem(string itemId)
    {
        return _isShowing && _currentItem != null && _currentItem.itemId == itemId;
    }

    /// <summary>
    /// Verifica si actualmente se está mostrando un ítem específico por instanceId.
    /// </summary>
    public bool IsShowingItemInstance(string instanceId)
    {
        return _isShowing && _currentItem != null && _currentItem.instanceId == instanceId;
    }

    /// <summary>
    /// Obtiene el estado actual del tooltip.
    /// </summary>
    public bool IsShowing => _isShowing;

    /// <summary>
    /// Obtiene el ítem actualmente mostrado.
    /// </summary>
    public InventoryItem CurrentItem => _currentItem;

    /// <summary>
    /// Obtiene los datos del ítem actualmente mostrado.
    /// </summary>
    public ItemDataSO CurrentItemData => _currentItemData;

    #endregion

    #region Private Methods

    /// <summary>
    /// Muestra el tooltip inmediatamente activando corrutina.
    /// </summary>
    private void ShowTooltipImmediate()
    {
        _controller.StartCoroutine(ShowTooltipCoroutine());
    }

    /// <summary>
    /// Corrutina que maneja la aparición del tooltip con layout correcto.
    /// </summary>
    private IEnumerator ShowTooltipCoroutine()
    {
        if (_controller.TooltipPanel != null)
        {
            _controller.TooltipPanel.SetActive(true);

            // Deshabilitar raycasting en el tooltip para evitar bucle infinito
            _controller.DisableRaycastTargets();
        }

        // Poblar contenido usando el renderer
        _controller.ContentRenderer?.PopulateContent(_currentItem, _currentItemData);

        // Esperar un frame para que el layout se calcule
        yield return null;

        // Forzar rebuild del layout
        _controller.PositioningSystem?.ForceLayoutRebuild();

        // Esperar otro frame para asegurar que todo esté calculado
        yield return null;

        // Posicionar el tooltip
        if (_lastMousePosition != Vector3.zero)
        {
            _controller.PositioningSystem?.UpdatePosition(_lastMousePosition);
        }

        _isShowing = true;
        _showTimer = 0f;
    }

    #endregion
}
