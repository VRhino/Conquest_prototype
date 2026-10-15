using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Data.Items;

public class InventoryItemCellController : BaseItemCellController
{
    // Drag & drop y cellIndex son específicos de inventario
    private InventoryDragHandler _dragHandler;
    [SerializeField] private int _cellIndex = -1;
    
    // Implementación de la propiedad abstracta de la base
    protected override System.Type InteractionType => typeof(InventoryItemCellInteraction);

    public override void Initialize()
    {
        base.Initialize();
        InitializeDragHandler();
    }

    private void InitializeDragHandler()
    {
            _dragHandler = GetComponent<InventoryDragHandler>();
            if (_dragHandler == null)
                _dragHandler = gameObject.AddComponent<InventoryDragHandler>();
    }

    public override void SetItem(InventoryItem item, ItemDataSO itemData)
    {
        base.SetItem(item, itemData);
        // Actualizar drag handler
        _dragHandler?.SetItemData(item, itemData, _cellIndex);
    }

    public override void Clear()
    {
        base.Clear();
        _dragHandler?.ClearItemData();
    }

    /// <summary>
    /// Establece el índice de esta celda en la grilla (solo inventario).
    /// </summary>
    public void SetCellIndex(int index)
    {
        _cellIndex = index;
        _dragHandler?.SetItemData(_currentItem, _currentItemData, _cellIndex);
    }

    public void ResetDefaultEvents()
    {
        if (_interaction is InventoryItemCellInteraction interaction)
        {
            interaction.ResetDefaultEvents();
        }
    }
}
