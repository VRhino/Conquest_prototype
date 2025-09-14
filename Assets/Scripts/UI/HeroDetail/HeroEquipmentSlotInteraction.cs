using UnityEngine;
using UnityEngine.EventSystems;
using Data.Items;
using UnityEditor.Graphs;
using System;
using BattleDrakeStudios.ModularCharacters;

/// <summary>
/// Maneja las interacciones del usuario con los slots de equipamiento del Hero Detail UI.
/// Se enfoca en desequipar items y mostrar tooltips para equipment slots.
/// Hereda comportamiento básico de event handling pero con lógica específica para equipment.
/// </summary>
public class HeroEquipmentSlotInteraction : BaseItemCellInteraction
{
     #region Events
    
    /// <summary>
    /// Evento transformado que incluye información del slot controller
    /// </summary>
    public System.Action<InventoryItem, ItemDataSO, HeroEquipmentSlotController> OnEquipmentSlotClicked;

    #endregion
    #region Private Fields
    private HeroEquipmentSlotController _slotController;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        // Obtener referencia al controller del slot
        _slotController = GetComponent<HeroEquipmentSlotController>();
        if (_slotController == null)
            Debug.LogError("[HeroEquipmentSlotInteraction] No se encontró HeroEquipmentSlotController en el mismo GameObject");
    }

    #region Event Forwarding

    /// <summary>
    /// Configura el sistema de event forwarding
    /// </summary>
    private void SetupEventForwarding()
    {
        // Suscribirse al evento base para hacer forwarding
        this.OnItemClicked += HandleItemClickedForwarding;
    }

    /// <summary>
    /// Método que intercepta el evento base y lo transforma en el nuevo evento
    /// </summary>
    private void HandleItemClickedForwarding(InventoryItem item, ItemDataSO itemData)
    {
        // Validar que tenemos todos los datos necesarios
        if ( _slotController == null)
        {
            Debug.LogWarning("[HeroEquipmentSlotInteraction] Datos inválidos para forwarding de evento");
            return;
        }
        
        // Log opcional para debugging
        Debug.Log($"[HeroEquipmentSlotInteraction] Forwarding click event for slot: {_slotController.SlotType}-{_slotController.SlotCategory}");
        
        // Lanzar el evento transformado con información adicional
        OnEquipmentSlotClicked?.Invoke(item, itemData, _slotController);
    }

    #endregion

    public void SetupEquipmentSlotEvents(
        Action<InventoryItem, ItemDataSO> onItemRightClicked,
        Action<InventoryItem, ItemDataSO, HeroEquipmentSlotController> onEquipmentSlotClicked = null)
    {
        base.SetEvents(HandleItemClickedForwarding, onItemRightClicked);
        OnEquipmentSlotClicked += onEquipmentSlotClicked;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Verifica si el slot tiene un item equipado actualmente.
    /// </summary>
    /// <returns>True si hay un item equipado</returns>
    public bool HasEquippedItem => _currentItem != null && _currentItemData != null;

    #endregion
}
