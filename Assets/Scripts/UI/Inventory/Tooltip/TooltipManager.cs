using System.Linq;
using System.Collections.Generic;
using Data.Items;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Owns hovered cell context, delayed display and comparison; refreshes on confirmed changes.</summary>
public class TooltipManager : MonoBehaviour
{
    [Header("Tooltip Settings")]
    [SerializeField] private float hoverDelay = 0.5f;
    [SerializeField] private bool enableTooltips = true;
    [SerializeField] private bool enableComparisonTooltips = false;
    [Header("Tooltip Controllers")]
    [SerializeField] private InventoryTooltipController primaryTooltipController;
    [SerializeField] private InventoryTooltipController secondaryTooltipController;
    private readonly Dictionary<string, BaseItemCellController> cells = new();
    private BaseItemCellController hoveredCell;

    private void Start()
    {
        if (primaryTooltipController == null) { enableTooltips = false; return; }
        primaryTooltipController.SetTooltipType(TooltipType.Primary);
        primaryTooltipController.SetShowDelay(hoverDelay);
        if (secondaryTooltipController != null)
        {
            secondaryTooltipController.SetTooltipType(TooltipType.Secondary);
            secondaryTooltipController.SetShowDelay(hoverDelay);
        }
    }
    private void OnEnable() => InventoryManager.OnInventoryChanged += ForceValidateTooltip;
    private void OnDisable()
    {
        InventoryManager.OnInventoryChanged -= ForceValidateTooltip;
        HideAllTooltips();
    }
    private void OnDestroy()
    {
        foreach (var cell in cells.Values.ToArray()) DisconnectCellFromTooltip(cell);
    }
    public void SetEnableComparisonTooltips(bool enable) => enableComparisonTooltips = enable;
    // Compatibility for callers: validation is now driven by inventory/cell events.
    public void SetValidationInterval(float interval) { }

    public void ConnectCellToTooltip(BaseItemCellController cell)
    {
        if (cell == null || cells.ContainsKey(cell.CellId)) return;
        cells[cell.CellId] = cell;
        cell.ConnectWithTooltips(OnItemHoverEnter, OnItemHoverExit, OnItemHoverMove, OnSetItem, OnClearItem);
    }
    public void DisconnectCellFromTooltip(BaseItemCellController cell)
    {
        if (cell == null) return;
        cells.Remove(cell.CellId);
        cell.DisconnectFromTooltips(OnItemHoverEnter, OnItemHoverExit, OnItemHoverMove, OnSetItem, OnClearItem);
        if (hoveredCell == cell) HideAllTooltips();
    }
    private void OnItemHoverEnter(InventoryItem item, ItemDataSO data, Vector3 position, string cellId)
    {
        if (!enableTooltips || item == null || data == null || !cells.TryGetValue(cellId, out hoveredCell)) return;
        ShowForContext(item, data, position, cellId, false);
    }
    private void OnItemHoverExit(InventoryItem item, ItemDataSO data, Vector3 position) => HideAllTooltips();
    private void OnItemHoverMove(InventoryItem item, ItemDataSO data, Vector3 position)
    {
        primaryTooltipController?.LifecycleManager?.UpdateMousePosition(position);
        secondaryTooltipController?.LifecycleManager?.UpdateMousePosition(position);
        if (primaryTooltipController != null && primaryTooltipController.IsShowing)
            primaryTooltipController.PositioningSystem.UpdatePosition(position);
        if (secondaryTooltipController != null && secondaryTooltipController.IsShowing)
            secondaryTooltipController.PositioningSystem.UpdatePosition(position);
    }
    private void OnSetItem(InventoryItem item, ItemDataSO data, string cellId)
    {
        if (hoveredCell == null || hoveredCell.CellId != cellId) return;
        ForceValidateTooltip();
    }
    public void OnClearItem(InventoryItem item, ItemDataSO data, string cellId)
    {
        if (hoveredCell != null && hoveredCell.CellId == cellId) HideAllTooltips();
    }
    public void ForceValidateTooltip()
    {
        if (hoveredCell == null) return;
        if (hoveredCell.CurrentItem == null || hoveredCell.CurrentItemData == null)
        { HideAllTooltips(); return; }
        ShowForContext(hoveredCell.CurrentItem, hoveredCell.CurrentItemData, GetMousePosition(),
            hoveredCell.CellId, primaryTooltipController != null && primaryTooltipController.IsShowing);
    }
    private void ShowForContext(InventoryItem item, ItemDataSO data, Vector3 position, string cellId, bool instant)
    {
        var context = hoveredCell != null ? hoveredCell.Context : ItemInteractionContext.Inventory;
        if (enableComparisonTooltips && context != ItemInteractionContext.Equipment &&
            context != ItemInteractionContext.Reward && ComparisonTooltipUtils.ShouldShowComparison(data))
            ShowDualTooltips(item, data, position, cellId, instant);
        else ShowSimpleTooltip(item, data, position, cellId, instant);
    }
    public void ShowSimpleTooltip(InventoryItem item, ItemDataSO data, Vector3 position, string cellId, bool instant = true)
    {
        if (!enableTooltips || primaryTooltipController == null) return;
        secondaryTooltipController?.HideTooltip();
        primaryTooltipController.SetInteractionContext(hoveredCell != null ? hoveredCell.Context : ItemInteractionContext.Inventory);
        primaryTooltipController.setDualSystem(false, null, null);
        Show(primaryTooltipController, item, data, position, cellId, instant);
    }
    public void ShowDualTooltips(InventoryItem item, ItemDataSO data, Vector3 position, string cellId, bool instant = true)
    {
        if (!enableTooltips || primaryTooltipController == null) return;
        var equipped = ComparisonTooltipUtils.GetEquippedItemForComparison(data.itemType, data.itemCategory);
        var definition = equipped != null ? ItemService.GetItemById(equipped.itemId) : null;
        if (secondaryTooltipController == null || secondaryTooltipController.tooltipPanel == null ||
            primaryTooltipController.tooltipPanel == null || equipped == null || definition == null)
        { ShowSimpleTooltip(item, data, position, cellId, instant); return; }
        var primary = (RectTransform)primaryTooltipController.tooltipPanel.transform;
        var secondary = (RectTransform)secondaryTooltipController.tooltipPanel.transform;
        primaryTooltipController.SetInteractionContext(hoveredCell != null ? hoveredCell.Context : ItemInteractionContext.Inventory);
        secondaryTooltipController.SetInteractionContext(ItemInteractionContext.Equipment);
        primaryTooltipController.setDualSystem(true, primary, secondary);
        secondaryTooltipController.setDualSystem(true, primary, secondary);
        Show(primaryTooltipController, item, data, position, cellId, instant);
        Show(secondaryTooltipController, equipped, definition, position, cellId, instant);
    }
    private static void Show(InventoryTooltipController tooltip, InventoryItem item, ItemDataSO definition,
        Vector3 position, string cellId, bool instant)
    {
        if (instant) tooltip.ShowTooltipInstant(item, definition, position, cellId);
        else tooltip.ShowTooltip(item, definition, position, cellId);
    }
    private Vector3 GetMousePosition() => Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : Vector3.zero;
    public void HideTooltipManual() => HideAllTooltips();
    public void HideAllTooltips()
    {
        primaryTooltipController?.HideTooltip();
        secondaryTooltipController?.HideTooltip();
        hoveredCell = null;
    }
    public bool ArePrimaryTooltipsActive() => primaryTooltipController != null && primaryTooltipController.IsShowing;
    public bool IsThisCellShowingTooltip(string id) => primaryTooltipController != null && primaryTooltipController.GetCellId() == id;
}
