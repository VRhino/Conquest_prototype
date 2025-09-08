using UnityEngine;
using UnityEngine.EventSystems;
using Data.Items;

/// <summary>
/// Maneja las interacciones del usuario con las celdas del inventario.
/// Permite equipar ítems, mostrar tooltips, etc.
/// </summary>
public class InventoryItemCellInteraction : BaseItemCellInteraction
{
    public void ResetDefaultEvents()
    {
        if (_OnRightClick != null) OnItemRightClicked -= _OnRightClick;

        _OnRightClick = HandleRightClickAction; 
        OnItemRightClicked += _OnRightClick;
    }

    public override void Initialize(string cellId)
    {
        base.Initialize(cellId);
        ResetDefaultEvents();
    }

    /// <summary>
    /// Intenta equipar el ítem actual.
    /// </summary>
    private void TryEquipItem()
    {
        if (_currentItem == null || _currentItemData == null) return;
        ItemData itemData = _currentItemData;
        bool success = InventoryManager.EquipItem(_currentItem);

        if (success)
            Debug.Log($"[InventoryItemCellInteraction] Ítem equipado: {itemData.name}");
        else
            Debug.LogWarning($"[InventoryItemCellInteraction] No se pudo equipar: {_currentItemData.name}");
    }

    /// <summary>
    /// Intenta usar el ítem consumible actual.
    /// </summary>
    private void TryUseConsumableItem()
    {
        if (_currentItem == null || _currentItemData == null) return;

        bool success = InventoryManager.UseConsumable(_currentItem);
        
        if (success)
            NotifyTooltipSystemAfterItemAction();
        else
            Debug.LogWarning($"[InventoryItemCellInteraction] No se pudo usar: {_currentItemData.name}");
    }

    /// <summary>
    /// Maneja la acción de click derecho según el tipo de ítem.
    /// </summary>
    private void HandleRightClickAction(InventoryItem item, ItemData itemData)
    {
        if (item == null || itemData == null) return;

        if (InventoryUtils.IsEquippableType(itemData.itemType))
            TryEquipItem();
        else if (InventoryUtils.IsConsumableType(itemData.itemType))
            TryUseConsumableItem();
        else
            Debug.LogWarning($"[InventoryItemCellInteraction] Tipo de ítem no soportado para click derecho: {itemData.itemType}");
    }

    /// <summary>
    /// Notifica al sistema de tooltips después de una acción que puede cambiar el inventario.
    /// Esto asegura que los tooltips se mantengan sincronizados.
    /// </summary>
    private void NotifyTooltipSystemAfterItemAction()
    {
        // Buscar el manager de tooltips
        TooltipManager tooltipManager = FindObjectOfType<TooltipManager>();
        if (tooltipManager != null)
        {
            // Forzar validación del tooltip actual
            tooltipManager.ForceValidateTooltip();
            Debug.Log("[InventoryItemCellInteraction] Tooltip system notified after item action");
        }
    }
}
