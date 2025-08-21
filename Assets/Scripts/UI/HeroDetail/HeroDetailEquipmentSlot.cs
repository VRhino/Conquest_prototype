using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Data.Items;

/// <summary>
/// Controlador especializado para slots de equipamiento en el Hero Detail UI.
/// Hereda de InventoryItemCellController para reutilizar toda la lógica de visualización,
/// y añade funcionalidad específica para equipment slots (placeholders, validaciones, etc.).
/// </summary>
public class HeroEquipmentSlotController : InventoryItemCellController
{
    #region Equipment Slot Properties
    
    [Header("Equipment Slot Configuration")]
    [SerializeField] private ItemType slotType = ItemType.None;
    [SerializeField] private ItemCategory slotCategory = ItemCategory.None;
    [SerializeField] private Sprite placeholderIcon;
    [SerializeField] private string placeholderText = "Empty Slot";
    
    // Referencias específicas para equipment slots
    [Header("Equipment Slot UI References")]
    [SerializeField] private GameObject placeholder;
    [SerializeField] private Image placeholderIconImage;
    [SerializeField] private TMP_Text placeholderLabel;
    
    // Componente de interacción - se inicializa dinámicamente
    private MonoBehaviour _interaction;

    #endregion

    #region Public Properties

    /// <summary>
    /// Tipo de slot de equipamiento (Weapon, Armor)
    /// </summary>
    public ItemType SlotType => slotType;

    /// <summary>
    /// Categoría específica del slot (Helmet, Torso, etc.)
    /// </summary>
    public ItemCategory SlotCategory => slotCategory;

    /// <summary>
    /// Indica si el slot tiene un item equipado actualmente
    /// </summary>
    public bool HasEquippedItem { get; private set; }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        // Don't call base.Awake() to prevent automatic InventoryItemCellInteraction addition
        // Instead, manually initialize only what we need from the base class
        
        // Initialize slot-specific properties if needed
        if (slotType == ItemType.Weapon && slotCategory == ItemCategory.None)
        {
            Debug.LogWarning($"HeroEquipmentSlotController '{name}': SlotCategory not set for Weapon slot");
        }
        else if (slotType == ItemType.Armor && slotCategory == ItemCategory.None)
        {
            Debug.LogWarning($"HeroEquipmentSlotController '{name}': SlotCategory not set for Armor slot");
        }
        
        // Note: We will add HeroEquipmentSlotInteraction in Start() instead of here
        // And we don't need to call InitializeDragHandler since it's private in base class
    }

    void Start()
    {
        // Llamar después de Awake para asegurar inicialización completa
        InitializeEquipmentSlot();
    }

    #endregion

    #region Equipment Slot Initialization

    /// <summary>
    /// Inicializa el slot de equipamiento con su tipo específico.
    /// </summary>
    /// <param name="equipmentSlotType">Tipo general del slot (Weapon, Armor)</param>
    /// <param name="equipmentSlotCategory">Categoría específica (Helmet, Torso, etc.)</param>
    public void InitializeSlot(ItemType equipmentSlotType, ItemCategory equipmentSlotCategory)
    {
        slotType = equipmentSlotType;
        slotCategory = equipmentSlotCategory;
        SetupPlaceholderVisuals();
        
        // Comenzar con slot vacío
        ShowPlaceholder();
        _interaction = GetComponent<HeroEquipmentSlotInteraction>();
        if (_interaction == null)
        {
            Debug.LogWarning($"[HeroEquipmentSlot] No interaction component found for {slotType}.{slotCategory} slot");
        }
        Debug.Log($"[HeroEquipmentSlot] Initialized {slotType}.{slotCategory} slot");
    }

    /// <summary>
    /// Configuración inicial del slot de equipamiento
    /// </summary>
    private void InitializeEquipmentSlot()
    {
        // Configurar placeholder si no está ya configurado
        if (placeholder == null)
        {
            Debug.LogWarning($"[HeroEquipmentSlot] Placeholder no configurado para slot {slotType}.{slotCategory}");
        }
        
        SetupPlaceholderVisuals();
    }

    #endregion

    #region Equipment Slot Visual Management

    /// <summary>
    /// Configura los elementos visuales del placeholder según el tipo de slot
    /// </summary>
    private void SetupPlaceholderVisuals()
    {
        if (placeholderIconImage != null && placeholderIcon != null)
        {
            placeholderIconImage.sprite = placeholderIcon;
        }
        
        if (placeholderLabel != null)
        {
            placeholderLabel.text = GetSlotDisplayName();
        }
    }

    /// <summary>
    /// Muestra el placeholder (slot vacío)
    /// </summary>
    private void ShowPlaceholder()
    {
        if (placeholder != null)
            placeholder.SetActive(true);
            
        if (itemPanel != null)
            itemPanel.SetActive(false);
            
        HasEquippedItem = false;
    }

    /// <summary>
    /// Oculta el placeholder y muestra el item equipado
    /// </summary>
    private void HidePlaceholder()
    {
        if (placeholder != null)
            placeholder.SetActive(false);
            
        if (itemPanel != null)
            itemPanel.SetActive(true);
            
        HasEquippedItem = true;
    }

    #endregion

    #region Equipment Operations

    /// <summary>
    /// Puebla el slot con el item actualmente equipado desde EquipmentManagerService
    /// </summary>
    public void PopulateFromEquippedItem()
    {
        if (!PlayerSessionService.HasHero)
        {
            Debug.LogWarning("[HeroEquipmentSlot] No hero selected");
            ShowPlaceholder();
            return;
        }

        // Inicializar EquipmentManagerService si es necesario
        EquipmentManagerService.Initialize(PlayerSessionService.SelectedHero);
        
        // Obtener item equipado usando EquipmentManagerService
        var equippedItem = EquipmentManagerService.GetEquippedItem(slotType, slotCategory);
        
        if (equippedItem != null)
        {
            var itemData = ItemDatabase.Instance?.GetItemDataById(equippedItem.itemId);
            if (itemData != null)
            {
                SetEquippedItem(equippedItem, itemData);
                Debug.Log($"[HeroEquipmentSlot] Populated {slotType}.{slotCategory} slot with {equippedItem.itemId}");
            }
            else
            {
                Debug.LogWarning($"[HeroEquipmentSlot] ItemData not found for {equippedItem.itemId}");
                ShowPlaceholder();
            }
        }
        else
        {
            // No hay item equipado
            ShowPlaceholder();
            Debug.Log($"[HeroEquipmentSlot] {slotType}.{slotCategory} slot is empty");
        }
    }

    /// <summary>
    /// Establece un item como equipado en este slot
    /// </summary>
    /// <param name="equippedItem">Item equipado</param>
    /// <param name="itemData">Datos del item</param>
    public void SetEquippedItem(InventoryItem equippedItem, ItemData itemData)
    {
        if (equippedItem == null || itemData == null)
        {
            ShowPlaceholder();
            return;
        }

        // Validar que el item corresponde a este tipo de slot
        if (!IsValidItemForSlot(itemData))
        {
            Debug.LogError($"[HeroEquipmentSlot] Item {itemData.id} is not valid for {slotType}.{slotCategory} slot");
            ShowPlaceholder();
            return;
        }

        // Usar la funcionalidad base de InventoryItemCellController
        SetItem(equippedItem, itemData);
        
        // Configurar estado específico de equipment slot
        HidePlaceholder();
        
        // Sincronizar con componente de interacción si existe
        SyncWithInteractionComponent(equippedItem, itemData);
        
        Debug.Log($"[HeroEquipmentSlot] Set equipped item {equippedItem.itemId} in {slotType}.{slotCategory} slot");
    }

    /// <summary>
    /// Limpia el slot y muestra el placeholder
    /// </summary>
    public void ClearEquippedItem()
    {
        // Usar funcionalidad base
        Clear();
        
        // Mostrar placeholder
        ShowPlaceholder();
        
        // Sincronizar con componente de interacción
        SyncWithInteractionComponent(null, null);
        
        Debug.Log($"[HeroEquipmentSlot] Cleared {slotType}.{slotCategory} slot");
    }

    #endregion

    #region Validation Methods

    /// <summary>
    /// Valida si un item puede ser equipado en este slot
    /// </summary>
    /// <param name="itemData">Datos del item a validar</param>
    /// <returns>True si el item es válido para este slot</returns>
    private bool IsValidItemForSlot(ItemData itemData)
    {
        if (itemData == null || !itemData.IsEquipment)
            return false;

        // Para weapons, solo verificar tipo
        if (slotType == ItemType.Weapon)
            return itemData.itemType == ItemType.Weapon;

        // Para armor, verificar tipo y categoría
        if (slotType == ItemType.Armor)
            return itemData.itemType == ItemType.Armor && itemData.itemCategory == slotCategory;

        return false;
    }

    /// <summary>
    /// Valida si el item actualmente equipado puede ser desequipado
    /// </summary>
    /// <returns>True si se puede desequipar</returns>
    public bool CanUnequipCurrentItem()
    {
        if (!HasEquippedItem)
            return false;

        // Verificar que hay espacio en el inventario
        return InventoryStorageService.HasSpace();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Obtiene el nombre de display para este tipo de slot
    /// </summary>
    /// <returns>Nombre legible del tipo de slot</returns>
    private string GetSlotDisplayName()
    {
        if (slotType == ItemType.Weapon)
            return "Weapon";
            
        return slotCategory switch
        {
            ItemCategory.Helmet => "Helmet",
            ItemCategory.Torso => "Chest",
            ItemCategory.Gloves => "Gloves", 
            ItemCategory.Pants => "Legs",
            ItemCategory.Boots => "Boots",
            _ => "Equipment"
        };
    }

    /// <summary>
    /// Refresca el contenido del slot
    /// </summary>
    public void RefreshSlot()
    {
        PopulateFromEquippedItem();
    }

    /// <summary>
    /// Sincroniza el estado del slot con el componente de interacción
    /// </summary>
    /// <param name="equippedItem">Item equipado (null para limpiar)</param>
    /// <param name="itemData">Datos del item (null para limpiar)</param>
    private void SyncWithInteractionComponent(InventoryItem equippedItem, ItemData itemData)
    {
        if (_interaction != null)
        {
            // Usar reflexión para evitar errores de compilación temporales
            var setEquippedItemMethod = _interaction.GetType().GetMethod("SetEquippedItem");
            if (setEquippedItemMethod != null)
            {
                setEquippedItemMethod.Invoke(_interaction, new object[] { equippedItem, itemData });
            }
        }
    }

    #endregion

    #region Debug Info

    /// <summary>
    /// Información de debug del slot
    /// </summary>
    public string GetDebugInfo()
    {
        return $"Equipment Slot [{slotType}.{slotCategory}] - HasItem: {HasEquippedItem} - CanUnequip: {CanUnequipCurrentItem()}";
    }

    #endregion
}
