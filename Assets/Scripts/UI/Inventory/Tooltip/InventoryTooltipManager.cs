using Data.Items;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manager que integra el sistema de tooltips con el inventario.
/// Se encarga de conectar las interacciones de las celdas con el tooltip.
/// Maneja tooltips primarios (inventario con comparación) y secundarios (equipado como referencia).
/// </summary>
public class InventoryTooltipManager : MonoBehaviour
{
    [Header("Tooltip Settings")]
    [SerializeField] private float hoverDelay = 0.5f;
    [SerializeField] private bool enableTooltips = true;
    [SerializeField] private bool enableComparisonTooltips = false;

    [Header("Tooltip Controllers")]
    [SerializeField] private InventoryTooltipController primaryTooltipController;   // Muestra item del inventario con comparación
    [SerializeField] private InventoryTooltipController secondaryTooltipController; // Muestra item equipado como referencia

    private InventoryPanelController _inventoryPanel;

    // Legacy reference para compatibilidad
    private InventoryTooltipController _tooltipController => primaryTooltipController;

    void Start()
    {
        InitializeManager();
        StartTooltipValidation();
        ValidateReferences();
    }

    void OnDestroy()
    {
        StopTooltipValidation();

        // Limpiar conexiones al destruir el manager
        if (_tooltipController != null)
        {
            _tooltipController.HideTooltip();
        }
    }

    public void SetEnableComparisonTooltips(bool enable)
    {
        enableComparisonTooltips = enable;
    }

    #region Tooltip Validation System

    // Sistema de validación que se deshabilita automáticamente para equipment tooltips
    // y se reactiva para tooltips del inventario para evitar que se oculten incorrectamente
    private bool _validationEnabled = true;
    private float _validationInterval = 0.2f; // Validar cada 200ms
    private float _lastValidationTime = 0f;

    /// <summary>
    /// Inicia el sistema de validación periódica de tooltips.
    /// </summary>
    private void StartTooltipValidation()
    {
        _validationEnabled = true;
        _lastValidationTime = Time.time;

    }

    /// <summary>
    /// Detiene el sistema de validación periódica de tooltips.
    /// </summary>
    private void StopTooltipValidation()
    {
        _validationEnabled = false;
    }

    void Update()
    {
        // Validación periódica de tooltips
        if (_validationEnabled && Time.time - _lastValidationTime >= _validationInterval)
        {
            ValidateCurrentTooltip();
            _lastValidationTime = Time.time;
        }
    }

    /// <summary>
    /// Valida el tooltip actual y lo actualiza/oculta según corresponda.
    /// </summary>
    private void ValidateCurrentTooltip()
    {
        if (_tooltipController != null && _tooltipController.IsShowing)
        {
            _tooltipController.ValidateAndRefreshTooltip();
        }
    }

    /// <summary>
    /// Fuerza una validación inmediata del tooltip.
    /// Útil para llamar después de operaciones que modifiquen el inventario.
    /// </summary>
    public void ForceValidateTooltip()
    {
        if (_tooltipController != null)
        {
            Debug.Log("[InventoryTooltipManager] Forcing tooltip validation");
            _tooltipController.ValidateAndRefreshTooltip();
        }
    }

    /// <summary>
    /// Configura el intervalo de validación periódica.
    /// </summary>
    /// <param name="interval">Intervalo en segundos (mínimo 0.1)</param>
    public void SetValidationInterval(float interval)
    {
        _validationInterval = Mathf.Max(0.1f, interval);
    }

    #endregion

    /// <summary>
    /// Inicializa el manager y conecta con los sistemas necesarios.
    /// </summary>
    private void InitializeManager()
    {
        // Validar que las referencias están asignadas
        if (primaryTooltipController == null)
        {
            Debug.LogError("[InventoryTooltipManager] primaryTooltipController no está asignado en el Inspector");
            enableTooltips = false;
            return;
        }

        if (enableComparisonTooltips && secondaryTooltipController == null)
        {
            Debug.LogWarning("[InventoryTooltipManager] secondaryTooltipController no está asignado. Los tooltips de comparación se deshabilitarán.");
            enableComparisonTooltips = false;
        }

        // Configurar el tooltip normal
        primaryTooltipController.SetTooltipType(TooltipType.Primary);
        primaryTooltipController.SetShowDelay(hoverDelay);

        // Configurar el tooltip de comparación si está habilitado
        if (enableComparisonTooltips && secondaryTooltipController != null)
        {
            secondaryTooltipController.SetTooltipType(TooltipType.Secondary);
            secondaryTooltipController.SetShowDelay(hoverDelay);
        }

        // Buscar el panel de inventario
        _inventoryPanel = FindObjectOfType<InventoryPanelController>();
        if (_inventoryPanel == null)
        {
            Debug.LogWarning("[InventoryTooltipManager] No se encontró InventoryPanelController en la escena");
            return;
        }

        // Conectar con las celdas existentes
        _inventoryPanel = FindObjectOfType<InventoryPanelController>();
        if (_inventoryPanel == null)
        {
            Debug.LogWarning("[InventoryTooltipManager] No se encontró InventoryPanelController en la escena");
            return;
        }

        // Conectar con las celdas existentes
        ConnectToInventoryCells();
    }

    /// <summary>
    /// Conecta el sistema de tooltips con las celdas del inventario.
    /// </summary>
    private void ConnectToInventoryCells()
    {
        if (!enableTooltips || _tooltipController == null) return;

        // Buscar todas las celdas de inventario
        InventoryItemCellController[] cells = FindObjectsOfType<InventoryItemCellController>();

        foreach (var cell in cells)
        {
            ConnectCellToTooltip(cell);
        }

    }

    /// <summary>
    /// Conecta una celda específica al sistema de tooltips.
    /// </summary>
    /// <param name="cell">Celda a conectar</param>
    public void ConnectCellToTooltip(InventoryItemCellController cell)
    {
        if (!enableTooltips || _tooltipController == null || cell == null) return;

        // Buscar o agregar el componente de interacción
        InventoryItemCellInteraction interaction = cell.GetComponent<InventoryItemCellInteraction>();
        if (interaction == null)
        {
            interaction = cell.gameObject.AddComponent<InventoryItemCellInteraction>();
        }

        // Conectar los eventos
        interaction.OnItemHoverEnter += OnItemHoverEnter;
        interaction.OnItemHoverExit += OnItemHoverExit;
        interaction.OnItemHoverMove += OnItemHoverMove;
        interaction.OnSetItem += OnSetItem;
        interaction.OnClearItem += OnClearItem;
    }

    /// <summary>
    /// Desconecta una celda del sistema de tooltips.
    /// </summary>
    /// <param name="cell">Celda a desconectar</param>
    public void DisconnectCellFromTooltip(InventoryItemCellController cell)
    {
        if (cell == null) return;

        InventoryItemCellInteraction interaction = cell.GetComponent<InventoryItemCellInteraction>();
        if (interaction != null)
        {
            interaction.OnItemHoverEnter -= OnItemHoverEnter;
            interaction.OnItemHoverExit -= OnItemHoverExit;
            interaction.OnItemHoverMove -= OnItemHoverMove;
            interaction.OnSetItem -= OnSetItem;
            interaction.OnClearItem -= OnClearItem;
        }
    }

    /// <summary>
    /// Callback cuando el mouse entra en una celda con ítem.
    /// </summary>
    /// <param name="item">Ítem del inventario</param>
    /// <param name="itemData">Datos del ítem</param>
    /// <param name="mousePosition">Posición del mouse en coordenadas de pantalla</param>
    public void OnItemHoverEnter(InventoryItem item, ItemData itemData, Vector3 mousePosition, string cellId)
    {
        if (!enableTooltips || item == null || itemData == null) return;

        StartTooltipValidation();
       // Determinar si mostrar dual tooltips o solo el normal
        if (enableComparisonTooltips && ComparisonTooltipUtils.ShouldShowComparison(itemData))
        {
            ShowDualTooltips(item, itemData, mousePosition, cellId);
        }
        else if (primaryTooltipController != null)
        {
            ShowSimpleTooltip(item, itemData, mousePosition, cellId);
        }
    }

    /// <summary>
    /// Callback cuando el mouse sale de una celda con ítem.
    /// </summary>
    /// <param name="item">Ítem del inventario</param>
    /// <param name="itemData">Datos del ítem</param>
    /// <param name="mousePosition">Posición del mouse en coordenadas de pantalla</param>
    public void OnItemHoverExit(InventoryItem item, ItemData itemData, Vector3 mousePosition)
    {
        if (!enableTooltips) return;

        HideAllTooltips();
    }

    /// <summary>
    /// Callback cuando el mouse se mueve sobre una celda con ítem.
    /// </summary>
    /// <param name="item">Ítem del inventario</param>
    /// <param name="itemData">Datos del ítem</param>
    /// <param name="mousePosition">Posición del mouse en coordenadas de pantalla</param>
    public void OnItemHoverMove(InventoryItem item, ItemData itemData, Vector3 mousePosition)
    {
        if (!enableTooltips) return;

        // Actualizar posición del tooltip primario (normal)
        if (primaryTooltipController != null && primaryTooltipController.IsShowing)
        {
            primaryTooltipController.PositioningSystem.UpdatePosition(mousePosition);
        }

        // Actualizar posición del tooltip secundario (con offset)
        if (secondaryTooltipController != null && secondaryTooltipController.IsShowing)
        {
            secondaryTooltipController.PositioningSystem.UpdatePosition(mousePosition);
        }
    }
    /// <summary>
    /// Callback cuando se establece un ítem en una celda ya sea por cambio o por unequip.
    /// </summary>
    /// <param name="item">Ítem del inventario</param>
    /// <param name="itemData">Datos del ítem</param>
    /// <param name="cellIdSetted">ID de la celda donde se establece el ítem</param>
    public void OnSetItem(InventoryItem item, ItemData itemData, string cellIdSetted)
    {
        if (!enableTooltips || !ArePrimaryTooltipsActive() || !IsThisCellShowingTooltip(cellIdSetted)) return;

        if (enableComparisonTooltips)
        {
            Debug.Log("[InventoryTooltipManager] Updating to dual tooltips on item set");
            ShowDualTooltips(item, itemData, GetMousePosition(), cellIdSetted);
        }
        else
        {
            Debug.Log("[InventoryTooltipManager] Updating to simple tooltip on item set");
            ShowSimpleTooltip(item, itemData, GetMousePosition(), cellIdSetted);
        }
    }

    public void OnClearItem(InventoryItem item, ItemData itemData, string cellIdCleared)
    {
        if (!enableTooltips || !ArePrimaryTooltipsActive() || !IsThisCellShowingTooltip(cellIdCleared)) return;
        HideAllTooltips();
    }

    /// <summary>
    /// Muestra un tooltip simple (primario) para un ítem.
    /// </summary>
    /// <param name="item">Ítem del inventario</param>
    /// <param name="itemData">Datos del ítem</param>
    /// <param name="mousePosition">Posición del mouse en coordenadas de pantalla</param>
    /// <param name="cellId">ID de la celda donde se encuentra el ítem</param>
    public void ShowSimpleTooltip(InventoryItem item, ItemData itemData, Vector3 mousePosition, string cellId)
    {
        if (!enableTooltips || primaryTooltipController == null) return;

        primaryTooltipController.setDualSystem(false, null, null);
        primaryTooltipController.ShowTooltipInstant(item, itemData, mousePosition, cellId);
    }

    /// <summary>
    /// Muestra tooltips duales (primario + secundario) para equipamiento con posición específica.
    /// </summary>
    /// <param name="item">Ítem del inventario</param>
    /// <param name="itemData">Datos del ítem</param>
    /// <param name="mousePosition">Posición específica del mouse</param>
    public void ShowDualTooltips(InventoryItem item, ItemData itemData, Vector3 mousePosition, string cellId)
    {
        if (!enableTooltips) return;
        RectTransform primaryTooltipRect = primaryTooltipController.tooltipPanel.GetComponent<RectTransform>();
        RectTransform secondaryTooltipRect = secondaryTooltipController.tooltipPanel.GetComponent<RectTransform>();

        // Mostrar tooltip primario (inventario) - posición normal
        if (primaryTooltipController != null)
        {
            primaryTooltipController.setDualSystem(true, primaryTooltipRect, secondaryTooltipRect);
            primaryTooltipController.ShowTooltipInstant(item, itemData, mousePosition, cellId);
        }

        // Mostrar tooltip secundario (equipado) - con offset de comparación
        if (enableComparisonTooltips && secondaryTooltipController != null &&
            ComparisonTooltipUtils.ShouldShowComparison(itemData))
        {
            var equippedItemData = GetEquippedItemData(itemData.itemType, itemData.itemCategory);
            if (equippedItemData.equippedItem != null && equippedItemData.itemData != null)
            {
                secondaryTooltipController.setDualSystem(true, primaryTooltipRect, secondaryTooltipRect);
                secondaryTooltipController.ShowTooltipInstant(
                    equippedItemData.equippedItem,
                    equippedItemData.itemData,
                    mousePosition,
                    cellId
                );
            }
            else
            {
                // Fallback: si no hay ítem equipado, ocultar tooltip de comparación
                Debug.LogWarning($"[InventoryTooltipManager] No se pudo obtener el ítem equipado para comparación del tipo {itemData.itemType}");
            }
        }
    }

    private Vector3 GetMousePosition()
    {
        // Obtener posición actual del mouse usando Input System
        if (Mouse.current != null)
        {
            return Mouse.current.position.ReadValue();
        }
        else
        {
            // Fallback si no hay mouse disponible
            return new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        }
    }

    /// <summary>
    /// Obtiene los datos completos (InventoryItem + ItemData) del ítem equipado para un tipo específico.
    /// </summary>
    /// <param name="equipmentType">Tipo de equipamiento</param>
    /// <returns>Tupla con el ítem equipado y sus datos, o null si no hay nada equipado</returns>
    private (InventoryItem equippedItem, ItemData itemData) GetEquippedItemData(ItemType equipmentType, ItemCategory itemCategory)
    {
        try
        {
            // Obtener el ítem equipado usando la utilidad existente
            var equippedItem = ComparisonTooltipUtils.GetEquippedItemForComparison(equipmentType, itemCategory);
            if (equippedItem == null)
            {
                return (null, null);
            }

            // Obtener los datos del ítem de la base de datos
            var itemData = ItemDatabase.Instance?.GetItemDataById(equippedItem.itemId);
            if (itemData == null)
            {
                Debug.LogWarning($"[InventoryTooltipManager] No se encontraron datos para el ítem equipado ID: {equippedItem.itemId}");
                return (null, null);
            }

            return (equippedItem, itemData);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[InventoryTooltipManager] Error al obtener datos del ítem equipado: {ex.Message}");
            return (null, null);
        }
    }

    /// <summary>
    /// Oculta el tooltip manualmente.
    /// </summary>
    public void HideTooltipManual()
    {
        HideAllTooltips();
    }

    /// <summary>
    /// Oculta todos los tooltips (normal y comparación).
    /// </summary>
    public void HideAllTooltips()
    {
        if (primaryTooltipController != null)
        {
            primaryTooltipController.HideTooltip();
        }

        if (secondaryTooltipController != null)
        {
            secondaryTooltipController.HideTooltip();
        }
    }
    #region Helper Functions

    public bool ArePrimaryTooltipsActive() => primaryTooltipController != null && primaryTooltipController.IsShowing;

    public bool IsThisCellShowingTooltip(string cellId) => primaryTooltipController != null && primaryTooltipController.GetCellId() == cellId;

    #endregion
    #region Inspector Debugging

    /// <summary>
    /// Valida las referencias y muestra información de debug.
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void ValidateReferences()
    {
        Debug.Log($"[InventoryTooltipManager] Referencias:" +
                  $"\n- Primary Tooltip: {(primaryTooltipController != null ? "✓ Asignado" : "✗ Faltante")}" +
                  $"\n- Secondary Tooltip: {(secondaryTooltipController != null ? "✓ Asignado" : "✗ Faltante")}" +
                  $"\n- Enable Tooltips: {enableTooltips}" +
                  $"\n- Enable Comparison: {enableComparisonTooltips}");
    }

    #endregion
}
