using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Data.Items;

/// <summary>
/// Maneja el sistema de drag and drop para los ítems del inventario.
/// Se debe agregar a cada celda del inventario para permitir arrastrar ítems.
/// </summary>
public class InventoryDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [Header("Drag Settings")]
    [SerializeField] private Canvas parentCanvas;
    [SerializeField] private GraphicRaycaster graphicRaycaster;
    
    private InventoryItemCellController _cellController;
    private InventoryItem _currentItem;
    private ItemDataSO _currentItemData;
    [SerializeField] private int _cellIndex;

    // Drag visual
    private GameObject _dragVisual;
    private Image _dragImage;
    private CanvasGroup _dragCanvasGroup;
    
    // Original state
    private Transform _originalParent;
    private Vector3 _originalPosition;
    private CanvasGroup _originalCanvasGroup;

    void Awake()
    {
        _cellController = GetComponent<InventoryItemCellController>();
        
        // Buscar el Canvas padre si no está asignado
        if (parentCanvas == null)
            parentCanvas = GetComponentInParent<Canvas>();
            
        // Buscar el GraphicRaycaster si no está asignado
        if (graphicRaycaster == null)
            graphicRaycaster = parentCanvas?.GetComponent<GraphicRaycaster>();
            
        // Crear CanvasGroup si no existe
        if (_originalCanvasGroup == null)
        {
            _originalCanvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (_originalCanvasGroup == null)
                _originalCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    /// <summary>
    /// Configura los datos del ítem para este drag handler.
    /// Debe llamarse cuando se asigna un ítem a la celda.
    /// </summary>
    /// <param name="item">Ítem del inventario</param>
    /// <param name="itemData">Datos del ítem</param>
    /// <param name="cellIndex">Índice de la celda en la grilla</param>
    public void SetItemData(InventoryItem item, ItemDataSO itemData, int cellIndex)
    {
        _cellIndex = cellIndex;
        
        if (item == null || itemData == null)
        {
            ClearItemData();
            return;
        }
        _currentItem = item;
        _currentItemData = itemData;
    }

    /// <summary>
    /// Limpia los datos del ítem cuando la celda se vacía.
    /// </summary>
    public void ClearItemData()
    {
        _currentItem = null;
        _currentItemData = null;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Solo permitir drag si hay un ítem
        if (_currentItem == null || _currentItemData == null)
            return;

        // NUEVO: Verificar si el drag & drop está habilitado según el filtro actual
        var inventoryPanel = FindObjectOfType<InventoryPanelController>();
        if (inventoryPanel != null && !inventoryPanel.CanPerformDragDrop())
        {
            Debug.LogWarning("[InventoryDragHandler] Drag & Drop is disabled when filters are active");
            return;
        }

        CreateDragVisual();
        
        // Hacer la celda original semi-transparente durante el drag
        if (_originalCanvasGroup != null)
            _originalCanvasGroup.alpha = 0.6f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_dragVisual != null)
        {
            // Seguir la posición del cursor
            Vector2 localPointerPosition;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentCanvas.transform as RectTransform,
                eventData.position,
                parentCanvas.worldCamera,
                out localPointerPosition);
                
            _dragVisual.transform.localPosition = localPointerPosition;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // Restaurar transparencia de la celda original
        if (_originalCanvasGroup != null)
            _originalCanvasGroup.alpha = 1f;

        // Buscar si se soltó sobre una celda válida
        InventoryDragHandler targetHandler = GetTargetHandler(eventData);

        if (targetHandler != null && targetHandler != this)
        {
            // Realizar el intercambio de ítems
            PerformItemSwap(targetHandler);
        }

        // Limpiar visual de drag
        DestroyDragVisual();
    }

    public void OnDrop(PointerEventData eventData)
    {
        // Este método se llama cuando otro objeto se suelta sobre esta celda
        // La lógica principal está en OnEndDrag del objeto que se está arrastrando
    }

    /// <summary>
    /// Crea el visual que sigue al cursor durante el drag.
    /// </summary>
    private void CreateDragVisual()
    {
        if (_cellController == null || _currentItemData == null)
            return;

        // Crear GameObject para el visual de drag
        _dragVisual = new GameObject("DragVisual");
        _dragVisual.transform.SetParent(parentCanvas.transform, false);
        
        // Agregar RectTransform
        RectTransform rectTransform = _dragVisual.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(72, 72); // Tamaño de la celda
        
        // Agregar CanvasGroup para controlar transparencia
        _dragCanvasGroup = _dragVisual.AddComponent<CanvasGroup>();
        _dragCanvasGroup.alpha = 0.8f;
        _dragCanvasGroup.blocksRaycasts = false; // No debe interferir con el raycast
        
        // Agregar Image y configurar sprite
        _dragImage = _dragVisual.AddComponent<Image>();
        _dragImage.sprite = _currentItemData.icon;
        // Aplicar color de rareza
        _dragImage.color = InventoryUtils.GetRarityColor(_currentItem);
        
        // Posicionar al frente
        _dragVisual.transform.SetAsLastSibling();
    }

    /// <summary>
    /// Destruye el visual de drag.
    /// </summary>
    private void DestroyDragVisual()
    {
        if (_dragVisual != null)
        {
            Destroy(_dragVisual);
            _dragVisual = null;
            _dragImage = null;
            _dragCanvasGroup = null;
        }
    }

    /// <summary>
    /// Busca el handler objetivo donde se soltó el ítem.
    /// </summary>
    /// <param name="eventData">Datos del evento de pointer</param>
    /// <returns>Handler objetivo o null si no se encontró uno válido</returns>
    private InventoryDragHandler GetTargetHandler(PointerEventData eventData)
    {
        // Realizar raycast para encontrar objetos bajo el cursor
        var raycastResults = new System.Collections.Generic.List<RaycastResult>();
        graphicRaycaster.Raycast(eventData, raycastResults);
        
        foreach (var result in raycastResults)
        {
            InventoryDragHandler handler = result.gameObject.GetComponent<InventoryDragHandler>();
            if (handler != null && handler != this)
                return handler;
        }
        
        return null;
    }

    /// <summary>
    /// Realiza el intercambio de ítems entre dos celdas.
    /// </summary>
    /// <param name="targetHandler">Handler de la celda objetivo</param>
    private void PerformItemSwap(InventoryDragHandler targetHandler)
    {
        if (targetHandler == null)
            return;

        // Obtener datos de ambas celdas
        var sourceIndex = _cellIndex;
        
        var targetIndex = targetHandler._cellIndex;

        // Notificar al InventoryPanelController sobre el intercambio
        var panelController = GetComponentInParent<InventoryPanelController>();
        if (panelController != null)
        {
            panelController.SwapItems(sourceIndex, targetIndex);
        }
        else
        {
            Debug.LogError("InventoryDragHandler: No se encontró InventoryPanelController padre");
        }
    }
}
