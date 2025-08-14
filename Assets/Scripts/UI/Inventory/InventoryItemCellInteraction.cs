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
            
            // Auto-equipar si es equipamiento
            if (IsEquippableItem(_currentItemData.itemType))
            {
                TryEquipItem();
            }
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

        bool success = InventoryService.EquipItem(_currentItem.itemId);
        
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
    /// Verifica si un tipo de ítem es equipable.
    /// </summary>
    /// <param name="itemType">Tipo de ítem</param>
    /// <returns>True si es equipable</returns>
    private bool IsEquippableItem(ItemType itemType)
    {
        return InventoryUtils.IsEquippableType(itemType);
    }
}
