using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Data.Items;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Controlador principal del tooltip del inventario refactorizado usando arquitectura de componentes.
/// Delega responsabilidades específicas a componentes internos mientras preserva todas las referencias del Inspector.
/// Reducido de ~971 líneas a ~160 líneas manteniendo toda la funcionalidad.
/// </summary>
public class InventoryTooltipController : MonoBehaviour
{

    [Header("Main Tooltip")]
    public GameObject tooltipPanel;
    public Canvas tooltipCanvas;

    [Header("Title Panel")]
    public GameObject titlePanel;
    public Image backgroundImage;
    public Image dividerImage;
    public TMP_Text title;
    public Image miniatureImage;

    [Header("Content Panel")]
    public GameObject contentPanel;
    public TMP_Text descriptionText;
    public TMP_Text armorText;
    public TMP_Text categoryText;
    public TMP_Text durabilityText;
    public TMP_Text priceText;

    [Header("Stats Panel")]
    public GameObject statsPanel;
    public Transform statsContainer;
    public GameObject statEntryPrefab;

    [Header("Interaction Panel")]
    public GameObject interactionPanel;
    public TMP_Text actionText;

    [Header("Background Sprites por Rareza")]
    [SerializeField] private Sprite commonBackgroundSprite;
    [SerializeField] private Sprite uncommonBackgroundSprite;
    [SerializeField] private Sprite rareBackgroundSprite;
    [SerializeField] private Sprite epicBackgroundSprite;
    [SerializeField] private Sprite legendaryBackgroundSprite;

    [Header("Tooltip Configuration")]
    [SerializeField] private TooltipType tooltipType = TooltipType.Primary;
    [SerializeField] private Vector2 comparisonPositionOffset = new Vector2(320f, 0f);

    [Header("Settings")]
    [SerializeField] private Vector2 tooltipOffset = new Vector2(15f, -15f);
    [SerializeField] private float showDelay = 0.2f;
    [SerializeField] private bool followMouse = true;

    // *** ARQUITECTURA DE COMPONENTES INTERNOS ***
    // Componentes especializados que manejan responsabilidades específicas
    private TooltipLifecycleManager _lifecycleManager;
    private TooltipPositioningSystem _positioningSystem;
    private TooltipContentRenderer _contentRenderer;
    private TooltipStatsSystem _statsSystem;
    private TooltipDataValidator _dataValidator;

    /// <summary>
    /// Tipo actual del tooltip configurado en este controlador.
    /// </summary>
    public TooltipType CurrentTooltipType => tooltipType;

    // *** PROPIEDADES PUBLICAS PARA COMPONENTES ***
    // Los componentes acceden a estas propiedades para realizar su trabajo
    public TooltipLifecycleManager LifecycleManager => _lifecycleManager;
    public TooltipPositioningSystem PositioningSystem => _positioningSystem;
    public TooltipContentRenderer ContentRenderer => _contentRenderer;
    public TooltipStatsSystem StatsSystem => _statsSystem;
    public TooltipDataValidator DataValidator => _dataValidator;

    // Propiedades para acceso de componentes a referencias del Inspector
    public GameObject TooltipPanel => tooltipPanel;
    public Canvas TooltipCanvas => tooltipCanvas;
    public GameObject TitlePanel => titlePanel;
    public Image BackgroundImage => backgroundImage;
    public Image DividerImage => dividerImage;
    public TMP_Text Title => title;
    public Image MiniatureImage => miniatureImage;
    public GameObject ContentPanel => contentPanel;
    public TMP_Text DescriptionText => descriptionText;
    public TMP_Text ArmorText => armorText;
    public TMP_Text CategoryText => categoryText;
    public TMP_Text DurabilityText => durabilityText;
    public TMP_Text PriceText => priceText;
    public GameObject StatsPanel => statsPanel;
    public Transform StatsContainer => statsContainer;
    public GameObject StatEntryPrefab => statEntryPrefab;
    public GameObject InteractionPanel => interactionPanel;
    public TMP_Text ActionText => actionText;
    public Vector2 TooltipOffset => tooltipOffset;
    public float ShowDelay => showDelay;
    public bool FollowMouse => followMouse;
    public float Separation = 10f;

    public RectTransform _primaryTooltipRect { private set; get; } = null;
    public RectTransform _secondaryTooltipRect { private set; get; } = null;

    // Sprites por rareza
    public Sprite CommonBackgroundSprite => commonBackgroundSprite;
    public Sprite UncommonBackgroundSprite => uncommonBackgroundSprite;
    public Sprite RareBackgroundSprite => rareBackgroundSprite;
    public Sprite EpicBackgroundSprite => epicBackgroundSprite;
    public Sprite LegendaryBackgroundSprite => legendaryBackgroundSprite;

    private bool _isUsingDualSystem = false;

    #region Unity Lifecycle

    void Awake()
    {
        InitializeComponents();
    }

    void Start()
    {
        //_lifecycleManager?.HideTooltip();
    }

    void Update()
    {
        _lifecycleManager?.UpdateTimer();
    }

    void OnDestroy()
    {
        CleanupComponents();
    }

    #endregion

    #region Component Management

    /// <summary>
    /// Inicializa todos los componentes del tooltip.
    /// </summary>
    private void InitializeComponents()
    {
        // Crear e inicializar componentes
        _lifecycleManager = new TooltipLifecycleManager();
        _positioningSystem = new TooltipPositioningSystem();
        _contentRenderer = new TooltipContentRenderer();
        _statsSystem = new TooltipStatsSystem();
        _dataValidator = new TooltipDataValidator();

        // Inicializar todos los componentes
        _lifecycleManager.Initialize(this);
        _positioningSystem.Initialize(this);
        _contentRenderer.Initialize(this);
        _statsSystem.Initialize(this);
        _dataValidator.Initialize(this);

        ValidateComponents();
    }

    /// <summary>
    /// Limpia todos los componentes al destruir el controller.
    /// </summary>
    private void CleanupComponents()
    {
        _lifecycleManager?.Cleanup();
        _positioningSystem?.Cleanup();
        _contentRenderer?.Cleanup();
        _statsSystem?.Cleanup();
        _dataValidator?.Cleanup();
    }

    #endregion

    #region Public API
    public void setDualSystem(bool isDual, RectTransform primaryTooltipRect, RectTransform secondaryTooltipRect) {       
        _isUsingDualSystem = isDual;
        _primaryTooltipRect = primaryTooltipRect;
        _secondaryTooltipRect = secondaryTooltipRect;
    }
    public bool isDual { get { return _isUsingDualSystem; } }
    public string GetCellId() { return _lifecycleManager?.CellId; }

    /// <summary>
    /// Muestra el tooltip para un ítem específico con delay y posición del mouse.
    /// </summary>
    /// <param name="cellId">ID de la celda</param>
    /// <param name="item">Ítem del inventario</param>
    /// <param name="itemData">Datos del ítem</param>
    /// <param name="mousePosition">Posición del mouse en coordenadas de pantalla</param>
    public void ShowTooltip(InventoryItem item, ItemDataSO itemData, Vector3 mousePosition, string cellId)
    {
        _lifecycleManager?.ShowTooltip(item, itemData, mousePosition, cellId);
    }

    /// <summary>
    /// Muestra el tooltip inmediatamente sin delay.
    /// </summary>
    public void ShowTooltipInstant(InventoryItem item, ItemDataSO itemData, string cellId)
    {
        _lifecycleManager?.ShowTooltipInstant(item, itemData, cellId);
    }

    /// <summary>
    /// Muestra el tooltip inmediatamente con posición específica.
    /// </summary>
    public void ShowTooltipInstant(InventoryItem item, ItemDataSO itemData, Vector3 mousePosition, string cellId)
    {
        _lifecycleManager?.ShowTooltipInstant(item, itemData, mousePosition, cellId);
    }

    /// <summary>
    /// Oculta el tooltip.
    /// </summary>
    public void HideTooltip()
    {
        _lifecycleManager?.HideTooltip();
    }

    /// <summary>
    /// Indica si el tooltip está actualmente visible.
    /// </summary>
    public bool IsShowing => _lifecycleManager?.IsShowing ?? false;

    /// <summary>
    /// Verifica si actualmente se está mostrando un ítem específico.
    /// </summary>
    /// <param name="itemId">ID del ítem a verificar</param>
    /// <returns>True si el tooltip está mostrando este ítem</returns>
    public bool IsShowingItem(string itemId)
    {
        return _lifecycleManager?.IsShowingItem(itemId) ?? false;
    }

    /// <summary>
    /// Verifica si actualmente se está mostrando un ítem específico por instanceId.
    /// </summary>
    /// <param name="instanceId">Instance ID del ítem a verificar</param>
    /// <returns>True si el tooltip está mostrando este ítem</returns>
    public bool IsShowingItemInstance(string instanceId)
    {
        return _lifecycleManager?.IsShowingItemInstance(instanceId) ?? false;
    }

    /// <summary>
    /// Valida si el tooltip actual sigue siendo válido y lo actualiza o oculta según corresponda.
    /// </summary>
    public void ValidateAndRefreshTooltip()
    {
        _dataValidator?.ValidateAndRefreshTooltip();
    }

    /// <summary>
    /// Valida y oculta el tooltip si el ítem especificado ya no es válido.
    /// </summary>
    /// <param name="removedItemId">ID del ítem que fue removido</param>
    public void ValidateTooltipForRemovedItem(string removedItemId)
    {
        _dataValidator?.ValidateTooltipForRemovedItem(removedItemId);
    }

    /// <summary>
    /// Configura el delay de aparición del tooltip.
    /// </summary>
    public void SetShowDelay(float delay)
    {
        showDelay = Mathf.Max(0f, delay);
    }

    /// <summary>
    /// Configura si el tooltip debe seguir el mouse.
    /// </summary>
    public void SetFollowMouse(bool follow)
    {
        followMouse = follow;
    }

    /// <summary>
    /// Configura el tipo de tooltip.
    /// </summary>
    /// <param name="type">Tipo de tooltip</param>
    public void SetTooltipType(TooltipType type)
    {
        tooltipType = type;
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Deshabilita el raycasting en todos los elementos UI del tooltip para evitar interferencia.
    /// CRÍTICO: Esto previene el bucle infinito donde el tooltip bloquea el hover de las celdas.
    /// </summary>
    public void DisableRaycastTargets()
    {
        if (tooltipPanel == null) return;

        // Deshabilitar raycasting en todos los Graphic (Image, Text, etc.) del tooltip
        UnityEngine.UI.Graphic[] graphics = tooltipPanel.GetComponentsInChildren<UnityEngine.UI.Graphic>(includeInactive: true);

        foreach (var graphic in graphics)
        {
            graphic.raycastTarget = false;
        }
    }

    /// <summary>
    /// Valida que todos los componentes estén asignados.
    /// </summary>
    private void ValidateComponents()
    {
        if (tooltipPanel == null) Debug.LogWarning("[InventoryTooltipController] tooltipPanel no asignado");
        if (titlePanel == null) Debug.LogWarning("[InventoryTooltipController] titlePanel no asignado");
        if (contentPanel == null) Debug.LogWarning("[InventoryTooltipController] contentPanel no asignado");
        if (interactionPanel == null) Debug.LogWarning("[InventoryTooltipController] interactionPanel no asignado");
        if (statsContainer == null) Debug.LogWarning("[InventoryTooltipController] statsContainer no asignado");
        if (statEntryPrefab == null) Debug.LogWarning("[InventoryTooltipController] statEntryPrefab no asignado");
    }

    #endregion
}
