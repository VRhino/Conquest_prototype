using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Data.Items;

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
    [SerializeField]private InventoryTooltipManager tooltipManager;

    #endregion

    #region Internal State

    // Dictionary para acceso rápido a slots por tipo
    private Dictionary<(ItemType, ItemCategory), HeroEquipmentSlotController> _slotMap;
    
    // Estado de inicialización
    private bool _isInitialized = false;
    
    // Referencia al héroe actual
    private HeroData _currentHero;

    #endregion

    #region Unity Lifecycle

    void Start()
    {
        if (autoInitializeOnStart)
        {
            InitializePanel();
        }
    }

    void OnEnable()
    {
        if (_isInitialized && autoPopulateOnEnable)
        {
            RefreshAllSlots();
        }
    }

    #endregion

    #region Panel Initialization

    /// <summary>
    /// Inicializa el panel de equipamiento y todos sus slots.
    /// </summary>
    public void InitializePanel()
    {
        if (_isInitialized)
        {
            Debug.LogWarning("[HeroEquipmentPanel] Panel already initialized");
            return;
        }

        Debug.Log("[HeroEquipmentPanel] Initializing equipment panel...");

        // Crear mapa de slots
        CreateSlotMap();
        
        // Inicializar cada slot individualmente
        InitializeIndividualSlots();
        
        // Configurar eventos de equipment change
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

        // Poblar cada slot con su item equipado correspondiente
        PopulateIndividualSlots();
    }

    /// <summary>
    /// Puebla cada slot individual desde el equipamiento del héroe.
    /// </summary>
    private void PopulateIndividualSlots()
    {
        // Poblar cada slot usando su método PopulateFromEquippedItem
        helmetSlot?.PopulateFromEquippedItem();
        torsoSlot?.PopulateFromEquippedItem();
        glovesSlot?.PopulateFromEquippedItem();
        pantsSlot?.PopulateFromEquippedItem();
        bootsSlot?.PopulateFromEquippedItem();
        weaponSlot?.PopulateFromEquippedItem();
        
        Debug.Log("[HeroEquipmentPanel] All slots populated from equipped items");
    }

    /// <summary>
    /// Refresca todos los slots con el estado actual del equipamiento.
    /// </summary>
    public void RefreshAllSlots()
    {
        if (!_isInitialized)
        {
            Debug.LogWarning("[HeroEquipmentPanel] Cannot refresh - panel not initialized");
            return;
        }

        PopulateFromSelectedHero();
    }

    /// <summary>
    /// Reconfigura el sistema de tooltips. Útil si el InventoryTooltipManager 
    /// se crea después de este panel o si cambia de ubicación.
    /// </summary>
    public void ReconfigureTooltipSystem()
    {
        if (!_isInitialized)
        {
            Debug.LogWarning("[HeroEquipmentPanel] Cannot reconfigure tooltips - panel not initialized");
            return;
        }

        // Limpiar integración actual
        CleanupTooltipIntegration();
        
        // Buscar nuevo tooltip manager
        tooltipManager = FindTooltipManager();
        
        // Reinicializar integración
        InitializeTooltipIntegration();
        
        Debug.Log("[HeroEquipmentPanel] Tooltip system reconfigured");
    }

    /// <summary>
    /// Limpia todos los slots y muestra placeholders.
    /// </summary>
    public void ClearAllSlots()
    {
        helmetSlot?.ClearEquippedItem();
        torsoSlot?.ClearEquippedItem();
        glovesSlot?.ClearEquippedItem();
        pantsSlot?.ClearEquippedItem();
        bootsSlot?.ClearEquippedItem();
        weaponSlot?.ClearEquippedItem();
        
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
        
        if (helmetSlot != null) slots.Add(helmetSlot);
        if (torsoSlot != null) slots.Add(torsoSlot);
        if (glovesSlot != null) slots.Add(glovesSlot);
        if (pantsSlot != null) slots.Add(pantsSlot);
        if (bootsSlot != null) slots.Add(bootsSlot);
        if (weaponSlot != null) slots.Add(weaponSlot);
        
        return slots;
    }

    /// <summary>
    /// Obtiene solo los slots de armor.
    /// </summary>
    /// <returns>Lista con slots de armor no nulos</returns>
    public List<HeroEquipmentSlotController> GetArmorSlots()
    {
        var armorSlots = new List<HeroEquipmentSlotController>();
        
        if (helmetSlot != null) armorSlots.Add(helmetSlot);
        if (torsoSlot != null) armorSlots.Add(torsoSlot);
        if (glovesSlot != null) armorSlots.Add(glovesSlot);
        if (pantsSlot != null) armorSlots.Add(pantsSlot);
        if (bootsSlot != null) armorSlots.Add(bootsSlot);
        
        return armorSlots;
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

    /// <summary>
    /// Maneja eventos de cambio de equipamiento.
    /// </summary>
    /// <param name="itemType">Tipo de item que cambió</param>
    /// <param name="itemCategory">Categoría que cambió</param>
    private void OnEquipmentChanged(ItemType itemType, ItemCategory itemCategory)
    {
        var changedSlot = GetSlot(itemType, itemCategory);
        if (changedSlot != null)
        {
            changedSlot.RefreshSlot();
            Debug.Log($"[HeroEquipmentPanel] Refreshed slot after equipment change: {itemType}.{itemCategory}");
        }
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
        
        if (helmetSlot == null) { Debug.LogError("[HeroEquipmentPanel] Helmet slot reference is null"); isValid = false; }
        if (torsoSlot == null) { Debug.LogError("[HeroEquipmentPanel] Torso slot reference is null"); isValid = false; }
        if (glovesSlot == null) { Debug.LogError("[HeroEquipmentPanel] Gloves slot reference is null"); isValid = false; }
        if (pantsSlot == null) { Debug.LogError("[HeroEquipmentPanel] Pants slot reference is null"); isValid = false; }
        if (bootsSlot == null) { Debug.LogError("[HeroEquipmentPanel] Boots slot reference is null"); isValid = false; }
        if (weaponSlot == null) { Debug.LogError("[HeroEquipmentPanel] Weapon slot reference is null"); isValid = false; }
        
        return isValid;
    }

    /// <summary>
    /// Obtiene información de debug del panel.
    /// </summary>
    /// <returns>String con información de debug</returns>
    public string GetDebugInfo()
    {
        if (!_isInitialized) return "Panel not initialized";
        
        var slotsWithItems = GetAllSlots().Count(slot => slot.HasEquippedItem);
        var totalSlots = GetAllSlots().Count;
        var tooltipStatus = tooltipManager != null ? "Connected" : "Not Found";
        
        return $"Equipment Panel - Initialized: {_isInitialized} - " +
               $"Slots: {totalSlots} - Equipped: {slotsWithItems} - " +
               $"Hero: {(_currentHero?.heroName ?? "None")} - " +
               $"Tooltip: {tooltipStatus}";
    }

    #endregion

    #region Unity Editor Support

    void OnDestroy()
    {
        UnsubscribeFromEquipmentEvents();
        CleanupTooltipIntegration();
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
        if (Application.isPlaying && _isInitialized)
        {
            ValidateSlotReferences();
        }
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

    #region Tooltip Integration

    /// <summary>
    /// Inicializa la integración con el sistema de tooltips.
    /// Conecta los eventos de hover de los equipment slots con el tooltip manager.
    /// </summary>
    private void InitializeTooltipIntegration()
    {
        Debug.Log("[HeroEquipmentPanel] Initializing tooltip integration...");
        
        // Buscar el tooltipManager dinámicamente
        if (tooltipManager == null)
        {
            tooltipManager = FindTooltipManager();
        }
        
        if (tooltipManager == null)
        {
            Debug.LogWarning("[HeroEquipmentPanel] TooltipManager could not be found. Tooltip functionality will not work.");
            return;
        }

        // Conectar eventos para cada slot
        ConnectSlotTooltipEvents(helmetSlot);
        ConnectSlotTooltipEvents(torsoSlot);
        ConnectSlotTooltipEvents(glovesSlot);
        ConnectSlotTooltipEvents(pantsSlot);
        ConnectSlotTooltipEvents(bootsSlot);
        ConnectSlotTooltipEvents(weaponSlot);

        Debug.Log("[HeroEquipmentPanel] Tooltip integration initialized successfully");
    }

    /// <summary>
    /// Conecta los eventos de tooltip para un slot específico.
    /// </summary>
    /// <param name="slot">Slot a conectar</param>
    private void ConnectSlotTooltipEvents(HeroEquipmentSlotController slot)
    {
        if (slot == null) return;
        
        // Obtener el componente de interacción del slot
        var interaction = slot.GetComponent<HeroEquipmentSlotInteraction>();

        if (interaction == null) return;
        // Conectar eventos de hover con métodos adapter
        interaction.OnSlotHoverEnter += OnSlotHoverEnter;
        interaction.OnSlotHoverMove += OnSlotHoverMove; 
        interaction.OnSlotHoverExit += OnSlotHoverExit;
    }

    /// <summary>
    /// Desconecta los eventos de tooltip para un slot específico.
    /// </summary>
    /// <param name="slot">Slot a desconectar</param>
    private void DisconnectSlotTooltipEvents(HeroEquipmentSlotController slot)
    {
        if (slot == null) return;

        var interaction = slot.GetComponent<HeroEquipmentSlotInteraction>();
        if (interaction == null) return;

        interaction.OnSlotHoverEnter -= OnSlotHoverEnter;
        interaction.OnSlotHoverMove -= OnSlotHoverMove;
        interaction.OnSlotHoverExit -= OnSlotHoverExit;
    }

    /// <summary>
    /// Adapter method: Convierte evento de equipment slot hover enter en evento de inventory tooltip.
    /// </summary>
    /// <param name="equippedItem">Item equipado en el slot</param>
    /// <param name="equippedItemData">Datos del item equipado</param>
    /// <param name="screenPosition">Posición en pantalla del hover</param>
    /// <param name="itemType">Tipo del item (Armor/Weapon)</param>
    /// <param name="itemCategory">Categoría del item (Helmet/Torso/etc)</param>
    private void OnSlotHoverEnter(InventoryItem equippedItem, ItemData equippedItemData, Vector3 screenPosition, ItemType itemType, ItemCategory itemCategory)
    {
        if (tooltipManager == null || equippedItem == null || equippedItemData == null) return;
        // Llamar al método público del tooltip manager con los parámetros adaptados
        tooltipManager.ShowEquipmentTooltip(equippedItem, equippedItemData, screenPosition);
    }

    /// <summary>
    /// Adapter method: Convierte evento de equipment slot hover move en evento de inventory tooltip.
    /// </summary>
    private void OnSlotHoverMove(InventoryItem equippedItem, ItemData equippedItemData, Vector3 screenPosition, ItemType itemType, ItemCategory itemCategory)
    {
        if (tooltipManager == null || equippedItem == null || equippedItemData == null) return;

        tooltipManager.UpdateEquipmentTooltipPosition(screenPosition);
    }

    /// <summary>
    /// Adapter method: Convierte evento de equipment slot hover exit en evento de inventory tooltip.
    /// </summary>
    private void OnSlotHoverExit(InventoryItem equippedItem, ItemData equippedItemData, Vector3 screenPosition, ItemType itemType, ItemCategory itemCategory)
    {
        if (tooltipManager == null) return;
        Debug.Log($"[HeroEquipmentPanel] Hiding tooltip for {itemType}.{itemCategory}");
        tooltipManager.HideEquipmentTooltip();
    }

    /// <summary>
    /// Busca el InventoryTooltipManager dinámicamente en la escena.
    /// </summary>
    /// <returns>El InventoryTooltipManager encontrado o null si no existe</returns>
    private InventoryTooltipManager FindTooltipManager()
    {
        // Primero buscar en el objeto padre (HeroDetailUIController)
        var parentTooltipManager = GetComponentInParent<InventoryTooltipManager>();
        if (parentTooltipManager != null)
        {
            Debug.Log("[HeroEquipmentPanel] Found tooltip manager in parent hierarchy");
            return parentTooltipManager;
        }

        // Buscar en los hijos del objeto actual
        var childTooltipManager = GetComponentInChildren<InventoryTooltipManager>();
        if (childTooltipManager != null)
        {
            Debug.Log("[HeroEquipmentPanel] Found tooltip manager in children");
            return childTooltipManager;
        }

        // Buscar globalmente en la escena
        var globalTooltipManager = FindObjectOfType<InventoryTooltipManager>();
        if (globalTooltipManager != null)
        {
            Debug.Log("[HeroEquipmentPanel] Found tooltip manager globally in scene");
            return globalTooltipManager;
        }

        // Si no se encuentra, intentar buscar en el Canvas del UI
        var canvasTooltipManager = FindInCanvas();
        if (canvasTooltipManager != null)
        {
            Debug.Log("[HeroEquipmentPanel] Found tooltip manager in Canvas");
            return canvasTooltipManager;
        }

        Debug.LogWarning("[HeroEquipmentPanel] No InventoryTooltipManager found in scene");
        return null;
    }

    /// <summary>
    /// Busca el InventoryTooltipManager en el Canvas que contiene este panel.
    /// </summary>
    /// <returns>El InventoryTooltipManager del Canvas o null</returns>
    private InventoryTooltipManager FindInCanvas()
    {
        var parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null)
        {
            var canvasTooltipManager = parentCanvas.GetComponentInChildren<InventoryTooltipManager>();
            return canvasTooltipManager;
        }
        return null;
    }

    /// <summary>
    /// Limpia la integración de tooltips desconectando eventos.
    /// </summary>
    private void CleanupTooltipIntegration()
    {
        DisconnectSlotTooltipEvents(helmetSlot);
        DisconnectSlotTooltipEvents(torsoSlot);
        DisconnectSlotTooltipEvents(glovesSlot);
        DisconnectSlotTooltipEvents(pantsSlot);
        DisconnectSlotTooltipEvents(bootsSlot);
        DisconnectSlotTooltipEvents(weaponSlot);

        Debug.Log("[HeroEquipmentPanel] Tooltip integration cleaned up");
    }

    #endregion

    #endregion
}
