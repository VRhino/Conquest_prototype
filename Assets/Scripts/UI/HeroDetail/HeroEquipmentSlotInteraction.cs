using UnityEngine;
using UnityEngine.EventSystems;
using Data.Items;
using UnityEditor.Graphs;

/// <summary>
/// Maneja las interacciones del usuario con los slots de equipamiento del Hero Detail UI.
/// Se enfoca en desequipar items y mostrar tooltips para equipment slots.
/// Hereda comportamiento básico de event handling pero con lógica específica para equipment.
/// </summary>
public class HeroEquipmentSlotInteraction : BaseItemCellInteraction
{
    #region Private Fields
    private HeroEquipmentSlotController _slotController;
    private ItemType _slotType => _slotController != null ? _slotController.SlotType : ItemType.None;
    private ItemCategory _slotCategory => _slotController != null ? _slotController.SlotCategory : ItemCategory.None;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        // Obtener referencia al controller del slot
        _slotController = GetComponent<HeroEquipmentSlotController>();
        if (_slotController == null)
            Debug.LogError("[HeroEquipmentSlotInteraction] No se encontró HeroEquipmentSlotController en el mismo GameObject");
    }

    void Start()
    {
        OnItemRightClicked += HandleRightClick;
        OnItemClicked += HandleLeftClick;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Verifica si el slot tiene un item equipado actualmente.
    /// </summary>
    /// <returns>True si hay un item equipado</returns>
    public bool HasEquippedItem => _currentItem != null && _currentItemData != null;
    
    #endregion

    #region Interaction Logic

    /// <summary>
    /// Maneja el click izquierdo en el slot.
    /// Para futuro: drag & drop, seleccionar desde inventario, etc.
    /// </summary>
    private void HandleLeftClick(InventoryItem item, ItemData itemData)
    {

        if (!HasEquippedItem)
        {
            Debug.Log($"[HeroEquipmentSlotInteraction] Left clicked on empty slot: {_slotType}.{_slotCategory}");
            // Futuro: abrir inventario filtrado, mostrar items compatibles, etc.
        }
    }

    /// <summary>
    /// Maneja el click derecho en el slot (desequipar item).
    /// </summary>
    private void HandleRightClick(InventoryItem item, ItemData itemData)
    {
        if (!HasEquippedItem) return;

        // Verificar si se puede desequipar
        if (!_slotController.CanUnequipCurrentItem()) return;

        // Intentar desequipar
        bool success = TryUnequipCurrentItem();


        if (success) Debug.Log($"[HeroEquipmentSlotInteraction] Successfully unequipped: {itemData.name}");
        else Debug.LogError($"[HeroEquipmentSlotInteraction] Failed to unequip: {itemData.name}");
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
            bool success = EquipmentManagerService.UnequipItem(_slotType, _slotCategory);

            if (success)
            {
                _slotController.Clear();
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
        var tooltipManager = FindObjectOfType<InventoryTooltipManager>();
        if (tooltipManager != null)
        {            
            Debug.Log("[HeroEquipmentSlotInteraction] Tooltip system notified after unequip action");
            tooltipManager.ForceValidateTooltip();
        }
    }

    #endregion
}
