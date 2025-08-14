using UnityEngine;
using UnityEngine.EventSystems;
using Data.Items;

/// <summary>
/// Maneja las interacciones del usuario con las celdas del inventario.
/// Permite equipar ítems, mostrar tooltips, etc.
/// </summary>
public class InventoryItemCellInteraction : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    private InventoryItem _currentItem;
    private ItemData _currentItemData;
    
    /// <summary>
    /// Callback cuando se hace clic en la celda.
    /// </summary>
    public System.Action<InventoryItem, ItemData> OnItemClicked;
    
    /// <summary>
    /// Callback cuando se hace clic derecho en la celda.
    /// </summary>
    public System.Action<InventoryItem, ItemData> OnItemRightClicked;
    
    /// <summary>
    /// Callback cuando el mouse entra en la celda.
    /// </summary>
    public System.Action<InventoryItem, ItemData, Vector3> OnItemHoverEnter;
    
    /// <summary>
    /// Callback cuando el mouse se mueve sobre la celda.
    /// </summary>
    public System.Action<InventoryItem, ItemData, Vector3> OnItemHoverMove;
    
    /// <summary>
    /// Callback cuando el mouse sale de la celda.
    /// </summary>
    public System.Action<InventoryItem, ItemData, Vector3> OnItemHoverExit;

    /// <summary>
    /// Asigna el ítem actual a esta celda para manejar las interacciones.
    /// </summary>
    /// <param name="item">Ítem del inventario</param>
    /// <param name="itemData">Datos del ítem</param>
    public void SetItem(InventoryItem item, ItemData itemData)
    {
        _currentItem = item;
        _currentItemData = itemData;
    }

    /// <summary>
    /// Limpia el ítem de esta celda.
    /// </summary>
    public void ClearItem()
    {
        _currentItem = null;
        _currentItemData = null;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_currentItem == null || _currentItemData == null) return;

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            OnItemClicked?.Invoke(_currentItem, _currentItemData);
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            OnItemRightClicked?.Invoke(_currentItem, _currentItemData);
            HandleRightClickAction(); // Nueva lógica centralizada
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_currentItem == null || _currentItemData == null) return;
        
        // Para UI Canvas, eventData.position ya está en coordenadas de pantalla correctas
        OnItemHoverEnter?.Invoke(_currentItem, _currentItemData, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_currentItem == null || _currentItemData == null) return;
        
        OnItemHoverExit?.Invoke(_currentItem, _currentItemData, eventData.position);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (_currentItem == null || _currentItemData == null) return;
        
        OnItemHoverMove?.Invoke(_currentItem, _currentItemData, eventData.position);
    }

    /// <summary>
    /// Intenta equipar el ítem actual.
    /// </summary>
    private void TryEquipItem()
    {
        if (_currentItem == null || _currentItemData == null) return;

        bool success = InventoryManager.EquipItem(_currentItem);
        
        if (success)
        {
            Debug.Log($"[InventoryItemCellInteraction] Ítem equipado: {_currentItemData.name}");
        }
        else
        {
            Debug.LogWarning($"[InventoryItemCellInteraction] No se pudo equipar: {_currentItemData.name}");
        }
    }

    /// <summary>
    /// Intenta usar el ítem consumible actual.
    /// </summary>
    private void TryUseConsumableItem()
    {
        if (_currentItem == null || _currentItemData == null) return;

        bool success = InventoryManager.UseConsumable(_currentItem);
        
        if (success)
        {            
            // Notificar al sistema de tooltips para validación
            NotifyTooltipSystemAfterItemAction();
        }
        else
        {
            Debug.LogWarning($"[InventoryItemCellInteraction] No se pudo usar: {_currentItemData.name}");
        }
    }

    /// <summary>
    /// Maneja la acción de click derecho según el tipo de ítem.
    /// </summary>
    private void HandleRightClickAction()
    {
        if (_currentItem == null || _currentItemData == null) return;

        if (InventoryUtils.IsEquippableType(_currentItemData.itemType))
        {
            TryEquipItem();
        }
        else if (InventoryUtils.IsConsumableType(_currentItemData.itemType))
        {
            TryUseConsumableItem();
        }
        else
        {
            Debug.LogWarning($"[InventoryItemCellInteraction] Tipo de ítem no soportado para click derecho: {_currentItemData.itemType}");
        }
    }

    /// <summary>
    /// Notifica al sistema de tooltips después de una acción que puede cambiar el inventario.
    /// Esto asegura que los tooltips se mantengan sincronizados.
    /// </summary>
    private void NotifyTooltipSystemAfterItemAction()
    {
        // Buscar el manager de tooltips
        InventoryTooltipManager tooltipManager = FindObjectOfType<InventoryTooltipManager>();
        if (tooltipManager != null)
        {
            // Forzar validación del tooltip actual
            tooltipManager.ForceValidateTooltip();
            Debug.Log("[InventoryItemCellInteraction] Tooltip system notified after item action");
        }
    }
}
