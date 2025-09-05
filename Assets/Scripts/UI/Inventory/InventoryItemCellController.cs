using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Data.Items;

public class InventoryItemCellController : BaseItemCellController
{
    // Drag & drop y cellIndex son específicos de inventario
    private Component _dragHandler;
    [SerializeField] private int _cellIndex = -1;

    protected override void Awake()
    {
        base.Awake();
        InitializeDragHandler();
    }


    private void InitializeDragHandler()
    {
        var dragHandlerType = System.Type.GetType("InventoryDragHandler");
        if (dragHandlerType != null)
        {
            _dragHandler = GetComponent(dragHandlerType);
            if (_dragHandler == null)
                _dragHandler = gameObject.AddComponent(dragHandlerType);
        }
    }

    public override void SetItem(InventoryItem item, ItemData itemData)
    {
        base.SetItem(item, itemData);
        // Actualizar drag handler
        CallDragHandlerMethod(item != null && itemData != null ? "SetItemData" : "ClearItemData", item, itemData, _cellIndex);
    }

    public override void Clear()
    {
        base.Clear();
        CallDragHandlerMethod("ClearItemData");
    }

    // Implementación de la propiedad abstracta de la base
    protected override System.Type InteractionType => typeof(InventoryItemCellInteraction);

    /// <summary>
    /// Establece el índice de esta celda en la grilla (solo inventario).
    /// </summary>
    public void SetCellIndex(int index)
    {
        _cellIndex = index;
        CallDragHandlerMethod("SetItemData", null, null, _cellIndex);
    }

    /// <summary>
    /// Llama un método del drag handler usando reflexión para evitar dependencias de compilación.
    /// </summary>
    private void CallDragHandlerMethod(string methodName, params object[] parameters)
    {
        if (_dragHandler == null) return;
        try
        {
            var method = _dragHandler.GetType().GetMethod(methodName);
            method?.Invoke(_dragHandler, parameters);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[InventoryItemCellController] Error calling drag handler method {methodName}: {ex.Message}");
        }
    }

    public void SetEvents(System.Action<InventoryItem, ItemData> onItemClicked, System.Action<InventoryItem, ItemData> onItemRightClicked)
    {
        _interaction?.SetEvents(onItemClicked, onItemRightClicked);
    }
    public void RemoveEvents()
    {
        _interaction?.RemoveEvents();
    }
    
    public void ResetDefaultEvents()
    {
        if (_interaction is InventoryItemCellInteraction interaction)
        {
            interaction.ResetDefaultEvents();
        }
    }
}