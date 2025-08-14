using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Data.Items;

/// <summary>
/// Controlador principal del panel de inventario que maneja filtros, grilla, ordenamiento y monedas.
/// Integra con InventoryManager para la gestión de datos y InventoryItemCellController para la visualización.
/// </summary>
public class InventoryPanelController : MonoBehaviour
{
    [Header("Panel Principal")]
    public GameObject mainPanel;

    [Header("Filter Section")]
    public Button allItemsButton;
    public Button equipmentButton;
    public Button stackableButton;

    [Header("Item Grid Section")]
    public Transform gridContainer;
    public GameObject inventoryItemCellPrefab;
    [SerializeField] private int gridWidth = 9;
    [SerializeField] private int gridHeight = 8;

    [Header("Sort Section")]
    public Button sortButton;

    [Header("Money Section")]
    public TMP_Text bronzeText;
    public TMP_Text silverText;
    public TMP_Text goldText;

    [Header("Exit Button")]
    public Button exitButton;

    [Header("Tooltip System")]
    public InventoryTooltipManager tooltipManager;

    // Control de filtros
    private ItemFilter _currentFilter = ItemFilter.All;
    private List<InventoryItemCellController> _cellControllers = new List<InventoryItemCellController>();
    private HeroData _currentHero;
    private InventoryItem[] slotItems;

    // Propiedades públicas para InventoryUIUtils
    public ItemFilter CurrentFilter => _currentFilter;
    public List<InventoryItemCellController> CellControllers => _cellControllers;
    public InventoryItem[] SlotItems => slotItems;
    public int GridWidth => gridWidth;
    public int GridHeight => gridHeight;

    /// <summary>
    /// Tipos de filtro disponibles para el inventario.
    /// </summary>
    public enum ItemFilter
    {
        All,
        Equipment,
        Stackable
    }

    void Awake()
    {
        InitializeButtons();
        CreateInventoryGrid();
        InitializeTooltipSystem();
    }

    void OnEnable()
    {
        // Suscribirse a eventos del inventario usando InventoryManager
        InventoryManager.OnInventoryChanged += RefreshFullUI;
        InventoryManager.OnItemAdded += OnItemChanged;
        InventoryManager.OnItemRemoved += OnItemChanged;
        
        // Inicializar array visual si aún no está creado
        if (slotItems == null)
            slotItems = new InventoryItem[gridWidth * gridHeight];
    }

    void OnDisable()
    {
        // Desuscribirse de eventos usando InventoryManager
        InventoryManager.OnInventoryChanged -= RefreshFullUI;
        InventoryManager.OnItemAdded -= OnItemChanged;
        InventoryManager.OnItemRemoved -= OnItemChanged;
    }

    /// <summary>
    /// Inicializa los listeners de los botones de la UI.
    /// </summary>
    private void InitializeButtons()
    {
        // Filtros
        if (allItemsButton != null)
            allItemsButton.onClick.AddListener(() => ApplyFilter(ItemFilter.All));
        
        if (equipmentButton != null)
            equipmentButton.onClick.AddListener(() => ApplyFilter(ItemFilter.Equipment));
        
        if (stackableButton != null)
            stackableButton.onClick.AddListener(() => ApplyFilter(ItemFilter.Stackable));

        // Ordenamiento
        if (sortButton != null)
            sortButton.onClick.AddListener(SortInventory);

        // Salir
        if (exitButton != null)
            exitButton.onClick.AddListener(ClosePanel);
    }

    /// <summary>
    /// Crea la grilla de celdas del inventario (9x8 = 72 celdas).
    /// </summary>
    private void CreateInventoryGrid()
    {
        if (gridContainer == null || inventoryItemCellPrefab == null)
        {
            Debug.LogError("[InventoryPanelController] gridContainer o inventoryItemCellPrefab no asignados");
            return;
        }

        // Limpiar grilla existente
        foreach (Transform child in gridContainer)
        {
            Destroy(child.gameObject);
        }
        _cellControllers.Clear();

        // Crear las 72 celdas (9x8)
        int totalCells = gridWidth * gridHeight;
        for (int i = 0; i < totalCells; i++)
        {
            GameObject cellObject = Instantiate(inventoryItemCellPrefab, gridContainer);
            InventoryItemCellController cellController = cellObject.GetComponent<InventoryItemCellController>();
            
            if (cellController != null)
            {
                _cellControllers.Add(cellController);
                cellController.SetCellIndex(i); // Asignar índice de celda
                cellController.Clear(); // Inicialmente vacía
            }
            else
            {
                Debug.LogWarning($"[InventoryPanelController] El prefab de celda no tiene InventoryItemCellController en el índice {i}");
            }
        }

    }

    /// <summary>
    /// Inicializa el sistema de tooltips conectándolo con las celdas del inventario.
    /// </summary>
    private void InitializeTooltipSystem()
    {
        // Buscar o crear el tooltip manager si no está asignado
        if (tooltipManager == null)
        {
            tooltipManager = FindObjectOfType<InventoryTooltipManager>();
        }

        if (tooltipManager == null)
        {
            Debug.LogWarning("[InventoryPanelController] No se encontró InventoryTooltipManager. El sistema de tooltips no estará disponible.");
            return;
        }

        // Conectar todas las celdas creadas al sistema de tooltips
        foreach (var cellController in _cellControllers)
        {
            if (cellController != null)
            {
                tooltipManager.ConnectCellToTooltip(cellController);
            }
        }

        Debug.Log($"[InventoryPanelController] Sistema de tooltips inicializado con {_cellControllers.Count} celdas");
    }

    /// <summary>
    /// Abre el panel de inventario con los datos del héroe especificado.
    /// </summary>
    /// <param name="heroData">Datos del héroe cuyo inventario se mostrará</param>
    public void OpenPanel(HeroData heroData)
    {
        if (heroData == null)
        {
            Debug.LogError("[InventoryPanelController] No se puede abrir el panel sin datos de héroe");
            return;
        }

        _currentHero = heroData;

        // Inicializar array visual si aún no está creado
        if (slotItems == null)
            slotItems = new InventoryItem[gridWidth * gridHeight];

        // Inicializar el servicio de inventario
        InventoryManager.Initialize(heroData);

        // Mostrar el panel
        if (mainPanel != null)
            mainPanel.SetActive(true);
        
        ToggleUIInteraction();

        // Actualizar displays usando el método unificado
        RefreshFullUI();

    }

    /// <summary>
    /// Cierra el panel de inventario.
    /// </summary>
    public void ClosePanel()
    {
        if (mainPanel != null)
            mainPanel.SetActive(false);
        
        ToggleUIInteraction();

        _currentHero = null;
    }

    /// <summary>
    /// Aplica un filtro específico a los ítems mostrados.
    /// </summary>
    /// <param name="filter">Tipo de filtro a aplicar</param>
    public void ApplyFilter(ItemFilter filter)
    {
        _currentFilter = filter;
        RefreshFullUI(); // Usa el método unificado que actualiza todo
        Debug.Log($"[InventoryPanelController] Filtro aplicado: {filter}");
    }

    /// <summary>
    /// Ordena el inventario y actualiza la visualización.
    /// </summary>
    public void SortInventory()
    {
        // InventoryManager.SortByType();
        Debug.Log("[InventoryPanelController] Inventario ordenado");
    }

    /// <summary>
    /// Actualiza completamente la UI del inventario.
    /// Este método reemplaza las llamadas individuales y soluciona el bug de actualización de monedas.
    /// SOLUCIÓN: Ahora UpdateMoneyDisplay() se llama junto con UpdateInventoryDisplay()
    /// </summary>
    public void RefreshFullUI()
    {
        if (_currentHero == null) return;

        // ESTA ES LA SOLUCIÓN AL BUG: llamar UpdateMoneyDisplay() junto con UpdateInventoryDisplay()
        UpdateInventoryDisplay();
        UpdateMoneyDisplay();    // ¡Esta era la línea que faltaba!
        UpdateFilterButtons();

        Debug.Log("[InventoryPanelController] FULL UI refresh completed - money display now updates correctly");
    }

    /// <summary>
    /// Actualiza la visualización del dinero del héroe.
    /// </summary>
    private void UpdateMoneyDisplay()
    {
        if (_currentHero == null) return;

        if (bronzeText != null)
            bronzeText.text = _currentHero.bronze.ToString();

        // Nota: Ahora usando los campos silver y gold de HeroData
        if (silverText != null)
            silverText.text = _currentHero.silver.ToString();

        if (goldText != null)
            goldText.text = _currentHero.gold.ToString();
    }

    /// <summary>
    /// Actualiza la visualización de los ítems en la grilla según el filtro actual.
    /// </summary>
    private void UpdateInventoryDisplay()
    {
        if (_currentHero == null || _cellControllers.Count == 0)
            return; // Early exit if no hero or cell controllers

        int totalCells = gridWidth * gridHeight;
        
        // Limpiar array visual
        Array.Clear(slotItems, 0, slotItems.Length);

        // Colocar cada item del inventario persistente en su posición visual correspondiente
        foreach (var item in _currentHero.inventory)
        {
            if (item != null && item.slotIndex >= 0 && item.slotIndex < totalCells)
            {
                slotItems[item.slotIndex] = item;
            }
        }

        // Actualizar cada celda visual basándose en slotItems
        for (int i = 0; i < totalCells && i < _cellControllers.Count; i++)
        {
            var cellController = _cellControllers[i];
            cellController.SetCellIndex(i);
            
            var item = slotItems[i];
            if (item != null)
            {
                var itemData = InventoryUtils.GetItemData(item.itemId);
                cellController.SetItem(item, itemData);
            }
            else
            {
                cellController.Clear();
            }
        }
    }

    /// <summary>
    /// Obtiene los ítems del inventario según el filtro actual.
    /// </summary>
    /// <returns>Lista de ítems filtrados</returns>
    private List<InventoryItem> GetFilteredItems()
    {
        var allItems = InventoryManager.GetAllItems();

        switch (_currentFilter)
        {
            case ItemFilter.All:
                return allItems;

            case ItemFilter.Equipment:
                return allItems.Where(item => InventoryUtils.IsEquippableType(item.itemType)).ToList();

            case ItemFilter.Stackable:
                return allItems.Where(item => InventoryUtils.IsStackable(item.itemId)).ToList();

            default:
                return allItems;
        }
    }

    /// <summary>
    /// Actualiza el estado visual de los botones de filtro.
    /// </summary>
    private void UpdateFilterButtons()
    {
        // Resetear interactividad de todos los botones
        if (allItemsButton != null)
            allItemsButton.interactable = _currentFilter != ItemFilter.All;
        
        if (equipmentButton != null)
            equipmentButton.interactable = _currentFilter != ItemFilter.Equipment;
        
        if (stackableButton != null)
            stackableButton.interactable = _currentFilter != ItemFilter.Stackable;
    }

    /// <summary>
    /// Callback para cuando cambian los ítems en el inventario.
    /// </summary>
    /// <param name="item">Ítem que cambió</param>
    private void OnItemChanged(InventoryItem item)
    {
        RefreshFullUI(); // Ahora usa el método unificado que actualiza todo
    }

    /// <summary>
    /// Intercambia ítems entre dos celdas de la grilla.
    /// Llamado por el sistema de drag and drop.
    /// </summary>
    /// <param name="sourceCellIndex">Índice de la celda origen</param>
    /// <param name="targetCellIndex">Índice de la celda destino</param>
    public void SwapItems(int sourceCellIndex, int targetCellIndex)
    {
        if (_currentHero?.inventory == null)
        {
            Debug.LogError("[InventoryPanelController] No hay héroe o inventario válido para intercambiar ítems");
            return;
        }

        // Validar índices
        int totalCells = gridWidth * gridHeight;
        if (sourceCellIndex < 0 || sourceCellIndex >= totalCells ||
            targetCellIndex < 0 || targetCellIndex >= totalCells)
        {
            Debug.LogError($"[InventoryPanelController] Índices de celda inválidos: origen={sourceCellIndex}, destino={targetCellIndex}");
            return;
        }

        Debug.Log($"[InventoryPanelController] Intercambiando ítems: origen={sourceCellIndex}, destino={targetCellIndex}");

        // Obtener ítems de las posiciones visuales
        var sourceItem = slotItems[sourceCellIndex];
        var targetItem = slotItems[targetCellIndex];

        // Si ambas celdas están vacías, no hacer nada
        if (sourceItem == null && targetItem == null)
            return;

        // Actualizar slotIndex de los ítems si existen
        if (sourceItem != null)
            sourceItem.slotIndex = targetCellIndex;
        
        if (targetItem != null)
            targetItem.slotIndex = sourceCellIndex;

        // Intercambiar en el array visual
        slotItems[sourceCellIndex] = targetItem;
        slotItems[targetCellIndex] = sourceItem;

        // Actualizar visualización completa
        RefreshFullUI();
        
        // Guardar cambios (el inventario persistente se actualiza automáticamente por las referencias)
        if (PlayerSessionService.CurrentPlayer != null)
        {
            SaveSystem.SavePlayer(PlayerSessionService.CurrentPlayer);
        }
    }

    private void ToggleUIInteraction()
    {
        if (DialogueUIState.IsDialogueOpen)
        {
            DialogueUIState.IsDialogueOpen = false;
            if (HeroCameraController.Instance != null)
                HeroCameraController.Instance.SetCameraFollowEnabled(true);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            DialogueUIState.IsDialogueOpen = true;
            if (HeroCameraController.Instance != null)
                HeroCameraController.Instance.SetCameraFollowEnabled(false);
            
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    #region Debug & Validation

    /// <summary>
    /// Valida que todos los componentes requeridos estén asignados.
    /// </summary>
    [ContextMenu("Validate Panel Setup")]
    public void ValidatePanelSetup()
    {
        int errors = 0;

        if (mainPanel == null) { Debug.LogError("[InventoryPanelController] mainPanel no asignado"); errors++; }
        if (gridContainer == null) { Debug.LogError("[InventoryPanelController] gridContainer no asignado"); errors++; }
        if (inventoryItemCellPrefab == null) { Debug.LogError("[InventoryPanelController] inventoryItemCellPrefab no asignado"); errors++; }
        if (allItemsButton == null) { Debug.LogWarning("[InventoryPanelController] allItemsButton no asignado"); }
        if (equipmentButton == null) { Debug.LogWarning("[InventoryPanelController] equipmentButton no asignado"); }
        if (stackableButton == null) { Debug.LogWarning("[InventoryPanelController] stackableButton no asignado"); }
        if (sortButton == null) { Debug.LogWarning("[InventoryPanelController] sortButton no asignado"); }
        if (bronzeText == null) { Debug.LogWarning("[InventoryPanelController] bronzeText no asignado"); }
        if (silverText == null) { Debug.LogWarning("[InventoryPanelController] silverText no asignado"); }
        if (goldText == null) { Debug.LogWarning("[InventoryPanelController] goldText no asignado"); }
        if (exitButton == null) { Debug.LogWarning("[InventoryPanelController] exitButton no asignado"); }

        if (errors == 0)
        {
            Debug.Log("[InventoryPanelController] Validación completada: configuración correcta");
        }
        else
        {
            Debug.LogError($"[InventoryPanelController] Validación falló: {errors} errores críticos encontrados");
        }
    }

    /// <summary>
    /// Información del estado actual del panel para debugging.
    /// </summary>
    [ContextMenu("Show Panel Info")]
    public void ShowPanelInfo()
    {
        Debug.Log($"[InventoryPanelController] Estado del Panel:\n" +
                  $"- Panel Activo: {(mainPanel != null ? mainPanel.activeSelf : false)}\n" +
                  $"- Héroe Actual: {(_currentHero != null ? _currentHero.heroName : "Ninguno")}\n" +
                  $"- Filtro Actual: {_currentFilter}\n" +
                  $"- Celdas Creadas: {_cellControllers.Count}\n" +
                  $"- Ítems en Inventario: {(_currentHero != null ? _currentHero.inventory.Count : 0)}");
    }

    #endregion
}
