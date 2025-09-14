using System;
using System.Collections.Generic;
using System.Linq;
using Data.Items;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controlador del panel de equipamiento del Hero Detail UI.
/// Coordina todos los slots de equipamiento individuales (Helmet, Torso, Gloves, Pants, Boots, Weapon).
/// Maneja la inicialización, población y actualización de los slots según el equipamiento actual del héroe.
/// </summary>
public class HeroEquipmentPanel : MonoBehaviour
{
    #region Equipment Slot References

    [Header("Equipment Slot References")]
    [SerializeField] private HeroEquipmentSlotController helmetSlot;
    [SerializeField] private HeroEquipmentSlotController torsoSlot;
    [SerializeField] private HeroEquipmentSlotController glovesSlot;
    [SerializeField] private HeroEquipmentSlotController pantsSlot;
    [SerializeField] private HeroEquipmentSlotController bootsSlot;
    [SerializeField] private HeroEquipmentSlotController weaponSlot;

    #endregion

    #region Panel Configuration

    [Header("Panel Configuration")]
    [SerializeField] private bool autoInitializeOnStart = true;
    [SerializeField] private bool autoPopulateOnEnable = true;

    #endregion

    #region Tooltip Integration

    [Header("Tooltip Integration")]
    [SerializeField] private TooltipManager tooltipManager;

    #endregion

    #region Internal State

    // Dictionary para acceso rápido a slots por tipo
    private Dictionary<(ItemType, ItemCategory), HeroEquipmentSlotController> _slotMap;

    // Estado de inicialización
    private bool _isInitialized = false;

    // Referencia al héroe actual
    private HeroData _currentHero;

    public void SetTooltipManager(TooltipManager manager)
    {
        if (manager != null)
        {
            tooltipManager = manager;
            manager.SetEnableComparisonTooltips(false);
        }
        else
        {
            tooltipManager?.HideAllTooltips();
            tooltipManager = null;
        }
    }

    public void SetEvents(
        Action<InventoryItem, ItemDataSO, HeroEquipmentSlotController> onEquipmentSlotClicked,
        Action<InventoryItem, ItemDataSO> onItemRightClicked
        )
    {
        foreach (var slot in GetAllSlots())
        {
            slot?.RemoveEvents();
            slot?.SetupEquipmentSlotEvents(onEquipmentSlotClicked, onItemRightClicked);
        }
    }

    #endregion

    #region Unity Lifecycle

    void Start() { if (autoInitializeOnStart) InitializePanel(); }

    void OnEnable() { if (_isInitialized && autoPopulateOnEnable) RefreshAllSlots(); }

    #endregion

    #region Panel Initialization

    /// <summary>
    /// Inicializa el panel de equipamiento y todos sus slots.
    /// </summary>
    public void InitializePanel()
    {
        if (_isInitialized) return;

        CreateSlotMap();
        InitializeIndividualSlots();
        SubscribeToEquipmentEvents();
        _isInitialized = true;

        Debug.Log("[HeroEquipmentPanel] Equipment panel initialized successfully");
    }

    /// <summary>
    /// Crea el mapa de slots para acceso rápido por tipo/categoría.
    /// </summary>
    private void CreateSlotMap()
    {
        _slotMap = new Dictionary<(ItemType, ItemCategory), HeroEquipmentSlotController>();

        // Mapear slots de armor
        if (helmetSlot != null) _slotMap[(ItemType.Armor, ItemCategory.Helmet)] = helmetSlot;
        if (torsoSlot != null) _slotMap[(ItemType.Armor, ItemCategory.Torso)] = torsoSlot;
        if (glovesSlot != null) _slotMap[(ItemType.Armor, ItemCategory.Gloves)] = glovesSlot;
        if (pantsSlot != null) _slotMap[(ItemType.Armor, ItemCategory.Pants)] = pantsSlot;
        if (bootsSlot != null) _slotMap[(ItemType.Armor, ItemCategory.Boots)] = bootsSlot;

        // Mapear slot de weapon
        if (weaponSlot != null) _slotMap[(ItemType.Weapon, ItemCategory.None)] = weaponSlot;

        Debug.Log($"[HeroEquipmentPanel] Created slot map with {_slotMap.Count} slots");
    }

    /// <summary>
    /// Inicializa cada slot individual con su tipo específico.
    /// </summary>
    private void InitializeIndividualSlots()
    {
        // Inicializar slots de armor
        helmetSlot?.InitializeSlot(ItemType.Armor, ItemCategory.Helmet);
        torsoSlot?.InitializeSlot(ItemType.Armor, ItemCategory.Torso);
        glovesSlot?.InitializeSlot(ItemType.Armor, ItemCategory.Gloves);
        pantsSlot?.InitializeSlot(ItemType.Armor, ItemCategory.Pants);
        bootsSlot?.InitializeSlot(ItemType.Armor, ItemCategory.Boots);

        // Inicializar slot de weapon
        weaponSlot?.InitializeSlot(ItemType.Weapon, ItemCategory.None);

        Debug.Log("[HeroEquipmentPanel] Individual slots initialized");
    }

    #endregion

    #region Equipment Population

    /// <summary>
    /// Puebla todos los slots con el equipamiento actual del héroe seleccionado.
    /// </summary>
    public void PopulateFromSelectedHero()
    {
        if (!PlayerSessionService.HasHero)
        {
            Debug.LogWarning("[HeroEquipmentPanel] No hero selected for equipment population");
            ClearAllSlots();
            return;
        }

        _currentHero = PlayerSessionService.SelectedHero;

        Debug.Log($"[HeroEquipmentPanel] Populating equipment for hero: {_currentHero.heroName}");

        PopulateIndividualSlots();
    }

    /// <summary>
    /// Puebla cada slot individual desde el equipamiento del héroe.
    /// </summary>
    private void PopulateIndividualSlots()
    {
        foreach (var slot in GetAllSlots())
        {
            slot?.PopulateFromEquippedItem();
        }
        Debug.Log("[HeroEquipmentPanel] All slots populated from equipped items");
    }

    /// <summary>
    /// Refresca todos los slots con el estado actual del equipamiento.
    /// </summary>
    public void RefreshAllSlots()
    {
        if (!_isInitialized) return;

        PopulateFromSelectedHero();
    }

    /// <summary>
    /// Limpia todos los slots y muestra placeholders.
    /// </summary>
    public void ClearAllSlots()
    {
        foreach (var slot in GetAllSlots())
        {
            slot?.Clear();
        }

        Debug.Log("[HeroEquipmentPanel] All slots cleared");
    }

    #endregion

    #region Slot Access Methods

    /// <summary>
    /// Obtiene un slot específico por tipo y categoría.
    /// </summary>
    /// <param name="itemType">Tipo de item (Weapon, Armor)</param>
    /// <param name="itemCategory">Categoría específica (Helmet, Torso, etc.)</param>
    /// <returns>El slot correspondiente o null si no existe</returns>
    public HeroEquipmentSlotController GetSlot(ItemType itemType, ItemCategory itemCategory)
    {
        if (_slotMap != null && _slotMap.TryGetValue((itemType, itemCategory), out var slot))
        {
            return slot;
        }

        Debug.LogWarning($"[HeroEquipmentPanel] Slot not found for {itemType}.{itemCategory}");
        return null;
    }

    /// <summary>
    /// Obtiene todos los slots de equipamiento.
    /// </summary>
    /// <returns>Lista con todos los slots no nulos</returns>
    public List<HeroEquipmentSlotController> GetAllSlots()
    {
        var slots = new List<HeroEquipmentSlotController>();
        foreach (var slot in _slotMap.Values)
        {
            if (slot != null) slots.Add(slot);
        }

        return slots;
    }

    #endregion

    #region Event System

    /// <summary>
    /// Se suscribe a eventos de cambios en equipamiento.
    /// </summary>
    private void SubscribeToEquipmentEvents()
    {
        // Suscribirse a eventos de EquipmentManagerService si existen
        // TODO: Implementar eventos de equipment change en EquipmentManagerService

        // Inicializar tooltip integration
        InitializeTooltipIntegration();

        Debug.Log("[HeroEquipmentPanel] Subscribed to equipment events");
    }

    /// <summary>
    /// Se desuscribe de eventos de equipamiento.
    /// </summary>
    private void UnsubscribeFromEquipmentEvents()
    {
        // TODO: Desuscribirse de eventos cuando se implementen

        Debug.Log("[HeroEquipmentPanel] Unsubscribed from equipment events");
    }

    #endregion

    #region Tooltip Integration

    /// <summary>
    /// Inicializa la integración con el sistema de tooltips.
    /// Conecta los eventos de hover de los equipment slots con el tooltip manager.
    /// </summary>
    private void InitializeTooltipIntegration()
    {
        Debug.Log("[HeroEquipmentPanel] Initializing tooltip integration...");

        // Buscar el tooltipManager dinámicamente
        if (tooltipManager == null) tooltipManager = FindTooltipManager();

        if (tooltipManager == null)
        {
            Debug.LogWarning("[HeroEquipmentPanel] TooltipManager could not be found. Tooltip functionality will not work.");
            return;
        }
        foreach (var slot in GetAllSlots())
        {
            ConnectSlotTooltipEvents(slot);
        }

        Debug.Log("[HeroEquipmentPanel] Tooltip integration initialized successfully");
    }

    /// <summary>
    /// Conecta los eventos de tooltip para un slot específico.
    /// </summary>
    /// <param name="slot">Slot a conectar</param>
    private void ConnectSlotTooltipEvents(HeroEquipmentSlotController slot)
    {
        if (slot == null) return;
        tooltipManager?.ConnectCellToTooltip(slot);
    }

    /// <summary>
    /// Desconecta los eventos de tooltip para un slot específico.
    /// </summary>
    /// <param name="slot">Slot a desconectar</param>
    private void DisconnectSlotTooltipEvents(HeroEquipmentSlotController slot)
    {
        if (slot == null) return;
        tooltipManager?.DisconnectCellFromTooltip(slot);
    }

    public RectTransform GetSlotTransform(ItemType itemType, ItemCategory itemCategory)
    {
        var slot = GetSlot(itemType, itemCategory);
        if (slot != null) return slot.GetComponent<RectTransform>();

        return null;
    }

    /// <summary>
    /// Busca el InventoryTooltipManager dinámicamente en la escena.
    /// </summary>
    /// <returns>El InventoryTooltipManager encontrado o null si no existe</returns>
    private TooltipManager FindTooltipManager()
    {
        // Buscar globalmente en la escena
        var globalTooltipManager = FindObjectOfType<TooltipManager>();
        if (globalTooltipManager != null)
        {
            Debug.Log("[HeroEquipmentPanel] Found tooltip manager globally in scene");
            return globalTooltipManager;
        }

        Debug.LogWarning("[HeroEquipmentPanel] No InventoryTooltipManager found in scene");
        return null;
    }

    /// <summary>
    /// Limpia la integración de tooltips desconectando eventos.
    /// </summary>
    private void CleanupTooltipIntegration()
    {
        foreach (var slot in GetAllSlots())
        {
            DisconnectSlotTooltipEvents(slot);
        }

        Debug.Log("[HeroEquipmentPanel] Tooltip integration cleaned up");
    }

    #endregion

    #region Validation and Debug

    /// <summary>
    /// Valida que todas las referencias de slots estén asignadas.
    /// </summary>
    /// <returns>True si todas las referencias están asignadas</returns>
    public bool ValidateSlotReferences()
    {
        bool isValid = true;
        foreach (var slot in GetAllSlots())
        {
            if (slot == null)
            {
                Debug.LogError("[HeroEquipmentPanel] One or more slot references are null");
                isValid = false;
            }
        }

        return isValid;
    }
    #endregion

    #region Unity Editor Support

    void OnDestroy()
    {
        UnsubscribeFromEquipmentEvents();
        if (tooltipManager != null) CleanupTooltipIntegration();
    }

#if UNITY_EDITOR
    /// <summary>
    /// Validación del editor para verificar referencias usando configuración por slot type/category.
    /// </summary>
    void OnValidate()
    {
        // Auto-asignar slots basado en su configuración de SlotType/SlotCategory
        AutoAssignSlotsByConfiguration();

        // Validar configuración en el editor
        if (Application.isPlaying && _isInitialized) ValidateSlotReferences();
    }

    /// <summary>
    /// Auto-asigna las referencias de slots basándose en su configuración de SlotType/SlotCategory
    /// en lugar de usar GetComponentInChildren que devuelve siempre el mismo.
    /// </summary>
    private void AutoAssignSlotsByConfiguration()
    {
        // Obtener todos los HeroEquipmentSlotController del panel
        var allSlots = GetComponentsInChildren<HeroEquipmentSlotController>(true);

        foreach (var slot in allSlots)
        {
            // Asignar según la configuración del slot
            if (slot.SlotType == ItemType.Armor)
            {
                switch (slot.SlotCategory)
                {
                    case ItemCategory.Helmet:
                        if (helmetSlot == null) helmetSlot = slot;
                        break;
                    case ItemCategory.Torso:
                        if (torsoSlot == null) torsoSlot = slot;
                        break;
                    case ItemCategory.Gloves:
                        if (glovesSlot == null) glovesSlot = slot;
                        break;
                    case ItemCategory.Pants:
                        if (pantsSlot == null) pantsSlot = slot;
                        break;
                    case ItemCategory.Boots:
                        if (bootsSlot == null) bootsSlot = slot;
                        break;
                }
            }
            else if (slot.SlotType == ItemType.Weapon)
            {
                // Para armas, podemos usar diferentes categorías si es necesario
                if (weaponSlot == null) weaponSlot = slot;
            }
        }
    }
#endif

    #endregion
}
