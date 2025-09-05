using UnityEngine;
using UnityEngine.EventSystems;
using Data.Items;

/// <summary>
/// Clase base abstracta para la interacción de celdas y slots de inventario/equipamiento.
/// Define la interfaz común para todos los tipos de interacción.
/// </summary>
public abstract class BaseItemCellInteraction : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    // Referencias al ítem actual y sus datos
    protected InventoryItem _currentItem;
    protected ItemData _currentItemData;

    protected System.Action<InventoryItem, ItemData> _OnClick;
    protected System.Action<InventoryItem, ItemData> _OnRightClick;
    protected string _cellId;

    // Callbacks de interacción (pueden ser asignados por el controlador)
    public System.Action<InventoryItem, ItemData> OnItemClicked;
    public System.Action<InventoryItem, ItemData> OnItemRightClicked;
    public System.Action<InventoryItem, ItemData, Vector3, string> OnItemHoverEnter;
    public System.Action<InventoryItem, ItemData, Vector3> OnItemHoverMove;
    public System.Action<InventoryItem, ItemData, Vector3> OnItemHoverExit;
    public System.Action<InventoryItem, ItemData, string> OnSetItem;
    public System.Action<InventoryItem, ItemData, string> OnClearItem;

    public void Initialize(string cellId)
    {
        _cellId = cellId;
    }

    protected virtual void Start()
    {
        OnItemClicked += _OnClick;
        OnItemRightClicked += _OnRightClick;
    }

    /// <summary>
    /// Asigna el ítem actual para manejar las interacciones.
    /// </summary>
    public virtual void SetItem(InventoryItem item, ItemData itemData, string cellId)
    {
        _currentItem = item;
        _currentItemData = itemData;
        OnSetItem?.Invoke(item, itemData, cellId);
    }

    public virtual void SetEvents(System.Action<InventoryItem, ItemData> onItemClicked, System.Action<InventoryItem, ItemData> onItemRightClicked)
    {
        _OnClick = onItemClicked;
        _OnRightClick = onItemRightClicked;
        OnItemClicked += onItemClicked;
        OnItemRightClicked += onItemRightClicked;
    }

    public virtual void RemoveEvents()
    {
        OnItemClicked -= _OnClick;
        OnItemRightClicked -= _OnRightClick;
        _OnClick = null;
        _OnRightClick = null;
    }

    /// <summary>
    /// Limpia el ítem de esta celda.
    /// </summary>
    public virtual void ClearItem()
    {
        _currentItem = null;
        _currentItemData = null;
        OnClearItem?.Invoke(_currentItem, _currentItemData, _cellId);
    }

    // Métodos de eventos de Unity
    public virtual void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            OnItemClicked?.Invoke(_currentItem, _currentItemData);
        else if (eventData.button == PointerEventData.InputButton.Right)
            OnItemRightClicked?.Invoke(_currentItem, _currentItemData);
    }

    public virtual void OnPointerEnter(PointerEventData eventData)
    {
        OnItemHoverEnter?.Invoke(_currentItem, _currentItemData, eventData.position, _cellId);
    }

    public virtual void OnPointerExit(PointerEventData eventData)
    {
        OnItemHoverExit?.Invoke(_currentItem, _currentItemData, eventData.position);
    }

    public virtual void OnPointerMove(PointerEventData eventData)
    {
        OnItemHoverMove?.Invoke(_currentItem, _currentItemData, eventData.position);
    }
}
