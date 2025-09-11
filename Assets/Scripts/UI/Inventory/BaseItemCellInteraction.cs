using UnityEngine;
using UnityEngine.EventSystems;
using Data.Items;
using System;

/// <summary>
/// Clase base abstracta para la interacción de celdas y slots de inventario/equipamiento.
/// Define la interfaz común para todos los tipos de interacción.
/// </summary>
public abstract class BaseItemCellInteraction : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    // Referencias al ítem actual y sus datos
    protected InventoryItem _currentItem;
    protected ItemData _currentItemData;

    protected Action<InventoryItem, ItemData> _OnClick;
    protected Action<InventoryItem, ItemData> _OnRightClick;
    protected string _cellId;

    // Callbacks de interacción (pueden ser asignados por el controlador)
    public Action<InventoryItem, ItemData> OnItemClicked;
    public Action<InventoryItem, ItemData> OnItemRightClicked;
    public Action<InventoryItem, ItemData, Vector3, string> OnItemHoverEnter;
    public Action<InventoryItem, ItemData, Vector3> OnItemHoverMove;
    public Action<InventoryItem, ItemData, Vector3> OnItemHoverExit;
    public Action<InventoryItem, ItemData, string> OnSetItem;
    public Action<InventoryItem, ItemData, string> OnClearItem;

    public virtual void Initialize(string cellId)
    {
        _cellId = cellId;
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

    public virtual void SetEvents(Action<InventoryItem, ItemData> onItemClicked, Action<InventoryItem, ItemData> onItemRightClicked)
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
        Debug.Log($"[BaseItemCellInteraction] ClearItem called for cellId={_cellId}");
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
    public virtual void ConnectWithTooltips(
        Action<InventoryItem, ItemData, Vector3, string> OnItemHoverEnter,
        Action<InventoryItem, ItemData, Vector3> OnItemHoverExit,
        Action<InventoryItem, ItemData, Vector3> OnItemHoverMove,
        Action<InventoryItem, ItemData, string> OnSetItem,
        Action<InventoryItem, ItemData, string> OnClearItem
    )
    {
        this.OnItemHoverEnter += OnItemHoverEnter;
        this.OnItemHoverExit += OnItemHoverExit;
        this.OnItemHoverMove += OnItemHoverMove;
        this.OnSetItem += OnSetItem;
        this.OnClearItem += OnClearItem;
    }
    public virtual void DisconnectFromTooltips(
        Action<InventoryItem, ItemData, Vector3, string> OnItemHoverEnter,
        Action<InventoryItem, ItemData, Vector3> OnItemHoverExit,
        Action<InventoryItem, ItemData, Vector3> OnItemHoverMove,
        Action<InventoryItem, ItemData, string> OnSetItem,
        Action<InventoryItem, ItemData, string> OnClearItem
    )
    {
        this.OnItemHoverEnter -= OnItemHoverEnter;
        this.OnItemHoverExit -= OnItemHoverExit;
        this.OnItemHoverMove -= OnItemHoverMove;
        this.OnSetItem -= OnSetItem;
        this.OnClearItem -= OnClearItem;
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
