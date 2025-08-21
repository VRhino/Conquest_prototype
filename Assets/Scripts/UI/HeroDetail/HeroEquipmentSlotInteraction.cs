using UnityEngine;
using UnityEngine.EventSystems;
using Data.Items;

/// <summary>
/// Maneja las interacciones del usuario con los slots de equipamiento del Hero Detail UI.
/// Se enfoca en desequipar items y mostrar tooltips para equipment slots.
/// Hereda comportamiento básico de event handling pero con lógica específica para equipment.
/// </summary>
public class HeroEquipmentSlotInteraction : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    #region Private Fields
    
    private HeroEquipmentSlotController _slotController;
    private InventoryItem _currentEquippedItem;
    private ItemData _currentEquippedItemData;
    
    #endregion

    #region Event Callbacks
    
    /// <summary>
    /// Callback cuando se hace clic izquierdo en el slot (equipar desde inventario - futuro uso)
    /// </summary>
    public System.Action<ItemType, ItemCategory> OnSlotLeftClicked;
    
    /// <summary>
    /// Callback cuando se hace clic derecho en el slot (desequipar item)
    /// </summary>
    public System.Action<InventoryItem, ItemData, ItemType, ItemCategory> OnSlotRightClicked;
    
    /// <summary>
    /// Callback cuando el mouse entra en el slot
    /// </summary>
    public System.Action<InventoryItem, ItemData, Vector3, ItemType, ItemCategory> OnSlotHoverEnter;
    
    /// <summary>
    /// Callback cuando el mouse se mueve sobre el slot
    /// </summary>
    public System.Action<InventoryItem, ItemData, Vector3, ItemType, ItemCategory> OnSlotHoverMove;
    
    /// <summary>
    /// Callback cuando el mouse sale del slot
    /// </summary>
    public System.Action<InventoryItem, ItemData, Vector3, ItemType, ItemCategory> OnSlotHoverExit;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        // Obtener referencia al controller del slot
        _slotController = GetComponent<HeroEquipmentSlotController>();
        
        if (_slotController == null)
        {
            Debug.LogError("[HeroEquipmentSlotInteraction] No se encontró HeroEquipmentSlotController en el mismo GameObject");
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Asigna el ítem actualmente equipado en este slot para manejar las interacciones.
    /// </summary>
    /// <param name="equippedItem">Item equipado (null si slot vacío)</param>
    /// <param name="equippedItemData">Datos del item equipado (null si slot vacío)</param>
    public void SetEquippedItem(InventoryItem equippedItem, ItemData equippedItemData)
    {
        _currentEquippedItem = equippedItem;
        _currentEquippedItemData = equippedItemData;
    }

    /// <summary>
    /// Limpia el ítem de este slot.
    /// </summary>
    public void ClearEquippedItem()
    {
        _currentEquippedItem = null;
        _currentEquippedItemData = null;
    }

    /// <summary>
    /// Verifica si el slot tiene un item equipado actualmente.
    /// </summary>
    /// <returns>True si hay un item equipado</returns>
    public bool HasEquippedItem => _currentEquippedItem != null && _currentEquippedItemData != null;

    #endregion

    #region Event System Implementation

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_slotController == null) return;
        
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            // Click izquierdo - para futuras implementaciones (drag & drop, equip desde inventario)
            OnSlotLeftClicked?.Invoke(_slotController.SlotType, _slotController.SlotCategory);
            HandleLeftClick();
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            // Click derecho - desequipar item actual
            if (HasEquippedItem)
            {
                OnSlotRightClicked?.Invoke(_currentEquippedItem, _currentEquippedItemData, 
                                        _slotController.SlotType, _slotController.SlotCategory);
                HandleRightClick();
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_slotController == null) return;
        // Notificar hover enter (tanto para slots vacíos como ocupados)
        OnSlotHoverEnter?.Invoke(_currentEquippedItem, _currentEquippedItemData, 
                               eventData.position, _slotController.SlotType, _slotController.SlotCategory);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_slotController == null) return;
        
        OnSlotHoverExit?.Invoke(_currentEquippedItem, _currentEquippedItemData, 
                              eventData.position, _slotController.SlotType, _slotController.SlotCategory);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (_slotController == null) return;
        
        OnSlotHoverMove?.Invoke(_currentEquippedItem, _currentEquippedItemData, 
                              eventData.position, _slotController.SlotType, _slotController.SlotCategory);
    }

    #endregion

    #region Interaction Logic

    /// <summary>
    /// Maneja el click izquierdo en el slot.
    /// Para futuro: drag & drop, seleccionar desde inventario, etc.
    /// </summary>
    private void HandleLeftClick()
    {
        if (HasEquippedItem)
        {
            Debug.Log($"[HeroEquipmentSlotInteraction] Left clicked on equipped item: {_currentEquippedItemData.name}");
            // Futuro: mostrar detalles, iniciar drag, etc.
        }
        else
        {
            Debug.Log($"[HeroEquipmentSlotInteraction] Left clicked on empty slot: {_slotController.SlotType}.{_slotController.SlotCategory}");
            // Futuro: abrir inventario filtrado, mostrar items compatibles, etc.
        }
    }

    /// <summary>
    /// Maneja el click derecho en el slot (desequipar item).
    /// </summary>
    private void HandleRightClick()
    {
        if (!HasEquippedItem)
        {
            Debug.Log($"[HeroEquipmentSlotInteraction] Right clicked on empty slot: {_slotController.SlotType}.{_slotController.SlotCategory}");
            return;
        }

        // Verificar si se puede desequipar
        if (!_slotController.CanUnequipCurrentItem())
        {
            Debug.LogWarning($"[HeroEquipmentSlotInteraction] Cannot unequip {_currentEquippedItemData.name} - no inventory space");
            // TODO: Mostrar mensaje al usuario
            return;
        }

        // Intentar desequipar
        bool success = TryUnequipCurrentItem();
        
        if (success)
        {
            Debug.Log($"[HeroEquipmentSlotInteraction] Successfully unequipped: {_currentEquippedItemData.name}");
        }
        else
        {
            Debug.LogError($"[HeroEquipmentSlotInteraction] Failed to unequip: {_currentEquippedItemData.name}");
        }
    }

    /// <summary>
    /// Intenta desequipar el item actual del slot.
    /// </summary>
    /// <returns>True si se desequipó exitosamente</returns>
    private bool TryUnequipCurrentItem()
    {
        if (!HasEquippedItem) return false;

        try
        {
            // Usar EquipmentManagerService para desequipar
            bool success = EquipmentManagerService.UnequipItem(_slotController.SlotType, _slotController.SlotCategory);
            
            if (success)
            {
                // Limpiar el slot visual
                _slotController.ClearEquippedItem();
                
                // Limpiar la referencia de interacción
                ClearEquippedItem();
                
                // Notificar al sistema de tooltips si es necesario
                NotifyTooltipSystemAfterAction();
                
                return true;
            }
            
            return false;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[HeroEquipmentSlotInteraction] Exception while unequipping item: {e.Message}");
            return false;
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Notifica al sistema de tooltips después de una acción que puede cambiar el equipamiento.
    /// </summary>
    private void NotifyTooltipSystemAfterAction()
    {
        // Buscar el manager de tooltips del Hero Detail
        // TODO: Implementar HeroDetailTooltipManager o reutilizar InventoryTooltipManager
        var tooltipManager = FindObjectOfType<InventoryTooltipManager>();
        if (tooltipManager != null)
        {
            tooltipManager.ForceValidateTooltip();
            Debug.Log("[HeroEquipmentSlotInteraction] Tooltip system notified after equipment action");
        }
    }

    /// <summary>
    /// Obtiene información de debug del slot.
    /// </summary>
    /// <returns>String con información de debug</returns>
    public string GetDebugInfo()
    {
        if (_slotController == null) return "No slot controller";
        
        return $"Equipment Slot Interaction [{_slotController.SlotType}.{_slotController.SlotCategory}] - " +
               $"HasItem: {HasEquippedItem} - " +
               $"ItemName: {(_currentEquippedItemData?.name ?? "None")}";
    }

    #endregion

    #region Debug

    /// <summary>
    /// Información de debug para el Inspector.
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void OnValidate()
    {
        if (_slotController == null)
        {
            _slotController = GetComponent<HeroEquipmentSlotController>();
        }
    }

    #endregion
}
