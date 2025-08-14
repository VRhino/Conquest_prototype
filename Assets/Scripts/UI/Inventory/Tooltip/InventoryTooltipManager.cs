using UnityEngine;
using Data.Items;

/// <summary>
/// Manager que integra el sistema de tooltips con el inventario.
/// Se encarga de conectar las interacciones de las celdas con el tooltip.
/// </summary>
public class InventoryTooltipManager : MonoBehaviour
{
    [Header("Tooltip Settings")]
    [SerializeField] private float hoverDelay = 0.5f;
    [SerializeField] private bool enableTooltips = true;
    
    private InventoryTooltipController _tooltipController;
    private InventoryPanelController _inventoryPanel;

    void Start()
    {
        InitializeManager();
        StartTooltipValidation();
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

    #region Tooltip Validation System

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
        
        Debug.Log("[InventoryTooltipManager] Sistema de validación de tooltips iniciado");
    }

    /// <summary>
    /// Detiene el sistema de validación periódica de tooltips.
    /// </summary>
    private void StopTooltipValidation()
    {
        _validationEnabled = false;
        
        Debug.Log("[InventoryTooltipManager] Sistema de validación de tooltips detenido");
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
        Debug.Log($"[InventoryTooltipManager] Intervalo de validación establecido a {_validationInterval}s");
    }

    #endregion

    /// <summary>
    /// Inicializa el manager y conecta con los sistemas necesarios.
    /// </summary>
    private void InitializeManager()
    {
        // Buscar el controlador de tooltip
        _tooltipController = InventoryTooltipController.Instance;
        if (_tooltipController == null)
        {
            _tooltipController = FindObjectOfType<InventoryTooltipController>(true);
        }

        if (_tooltipController == null)
        {
            Debug.LogWarning("[InventoryTooltipManager] No se encontró InventoryTooltipController en la escena");
            enableTooltips = false;
            return;
        }

        // Configurar delay del tooltip
        _tooltipController.SetShowDelay(hoverDelay);

        // Buscar el panel de inventario
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

        Debug.Log($"[InventoryTooltipManager] Conectadas {cells.Length} celdas al sistema de tooltips");
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
        }
    }

    /// <summary>
    /// Callback cuando el mouse entra en una celda con ítem.
    /// </summary>
    /// <param name="item">Ítem del inventario</param>
    /// <param name="itemData">Datos del ítem</param>
    /// <param name="mousePosition">Posición del mouse en coordenadas de pantalla</param>
    private void OnItemHoverEnter(InventoryItem item, ItemData itemData, Vector3 mousePosition)
    {
        if (!enableTooltips || _tooltipController == null) return;

        _tooltipController.ShowTooltip(item, itemData, mousePosition);
    }

    /// <summary>
    /// Callback cuando el mouse sale de una celda con ítem.
    /// </summary>
    /// <param name="item">Ítem del inventario</param>
    /// <param name="itemData">Datos del ítem</param>
    /// <param name="mousePosition">Posición del mouse en coordenadas de pantalla</param>
    private void OnItemHoverExit(InventoryItem item, ItemData itemData, Vector3 mousePosition)
    {
        if (!enableTooltips || _tooltipController == null) return;

        _tooltipController.HideTooltip();
    }

    /// <summary>
    /// Callback cuando el mouse se mueve sobre una celda con ítem.
    /// </summary>
    /// <param name="item">Ítem del inventario</param>
    /// <param name="itemData">Datos del ítem</param>
    /// <param name="mousePosition">Posición del mouse en coordenadas de pantalla</param>
    private void OnItemHoverMove(InventoryItem item, ItemData itemData, Vector3 mousePosition)
    {
        if (!enableTooltips || _tooltipController == null) return;

        Debug.Log($"[InventoryTooltipManager] Mouse move: {mousePosition}");
        _tooltipController.UpdateTooltipPosition(mousePosition);
    }

    /// <summary>
    /// Habilita o deshabilita el sistema de tooltips.
    /// </summary>
    /// <param name="enable">True para habilitar, false para deshabilitar</param>
    public void SetTooltipsEnabled(bool enable)
    {
        enableTooltips = enable;
        
        if (!enable && _tooltipController != null)
        {
            _tooltipController.HideTooltip();
        }
    }

    /// <summary>
    /// Configura el delay de aparición de los tooltips.
    /// </summary>
    /// <param name="delay">Tiempo de delay en segundos</param>
    public void SetHoverDelay(float delay)
    {
        hoverDelay = Mathf.Max(0f, delay);
        
        if (_tooltipController != null)
        {
            _tooltipController.SetShowDelay(hoverDelay);
        }
    }

    /// <summary>
    /// Fuerza la actualización de conexiones con las celdas.
    /// Útil cuando se crean nuevas celdas dinámicamente.
    /// </summary>
    public void RefreshCellConnections()
    {
        // Por ahora usar método local, luego migrar a InventoryUIUtils
        ConnectToInventoryCells();
    }

    /// <summary>
    /// Muestra un tooltip específico manualmente (para testing).
    /// </summary>
    /// <param name="item">Ítem a mostrar</param>
    /// <param name="itemData">Datos del ítem</param>
    public void ShowTooltipManual(InventoryItem item, ItemData itemData)
    {
        if (_tooltipController != null)
        {
            _tooltipController.ShowTooltipInstant(item, itemData);
        }
    }

    /// <summary>
    /// Oculta el tooltip manualmente.
    /// </summary>
    public void HideTooltipManual()
    {
        if (_tooltipController != null)
        {
            _tooltipController.HideTooltip();
        }
    }

    #region Inspector Debugging

    [ContextMenu("Test Show Tooltip")]
    private void TestShowTooltip()
    {
        if (!Application.isPlaying) return;

        // Buscar un ítem para testing
        if (InventoryManager.GetCurrentHero()?.inventory != null && InventoryManager.GetCurrentHero().inventory.Count > 0)
        {
            var firstItem = InventoryManager.GetCurrentHero().inventory[0];
            var itemData = ItemDatabase.Instance?.GetItemDataById(firstItem.itemId);
            
            if (itemData != null)
            {
                ShowTooltipManual(firstItem, itemData);
            }
        }
        else
        {
            Debug.Log("[InventoryTooltipManager] No hay ítems en el inventario para testing");
        }
    }

    [ContextMenu("Test Hide Tooltip")]
    private void TestHideTooltip()
    {
        if (!Application.isPlaying) return;
        HideTooltipManual();
    }

    [ContextMenu("Refresh Connections")]
    private void TestRefreshConnections()
    {
        if (!Application.isPlaying) return;
        RefreshCellConnections();
    }

    #endregion
}
