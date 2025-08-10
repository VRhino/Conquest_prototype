using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Data.Items;

/// <summary>
/// Controlador principal del panel de inventario que maneja filtros, grilla, ordenamiento y monedas.
/// Integra con InventoryService para la gestión de datos y InventoryItemCellController para la visualización.
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

    // Control de filtros
    private ItemFilter _currentFilter = ItemFilter.All;
    private List<InventoryItemCellController> _cellControllers = new List<InventoryItemCellController>();
    private HeroData _currentHero;

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
    }

    void OnEnable()
    {
        // Suscribirse a eventos del inventario
        InventoryEvents.OnInventoryChanged += UpdateInventoryDisplay;
        InventoryEvents.OnItemAdded += OnItemChanged;
        InventoryEvents.OnItemRemoved += OnItemChanged;
        InventoryEvents.OnInventorySorted += UpdateInventoryDisplay;
    }

    void OnDisable()
    {
        // Desuscribirse de eventos
        InventoryEvents.OnInventoryChanged -= UpdateInventoryDisplay;
        InventoryEvents.OnItemAdded -= OnItemChanged;
        InventoryEvents.OnItemRemoved -= OnItemChanged;
        InventoryEvents.OnInventorySorted -= UpdateInventoryDisplay;
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

        // Inicializar el servicio de inventario
        InventoryService.Initialize(heroData);

        // Mostrar el panel
        if (mainPanel != null)
            mainPanel.SetActive(true);
        
        ToggleUIInteraction();

        // Actualizar displays
        UpdateMoneyDisplay();
        UpdateInventoryDisplay();
        UpdateFilterButtons();

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
        UpdateInventoryDisplay();
        UpdateFilterButtons();
        Debug.Log($"[InventoryPanelController] Filtro aplicado: {filter}");
    }

    /// <summary>
    /// Ordena el inventario y actualiza la visualización.
    /// </summary>
    public void SortInventory()
    {
        InventoryService.SortByType();
        Debug.Log("[InventoryPanelController] Inventario ordenado");
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
            return;

        // Asegurarse que el inventario tenga el tamaño de la grilla (permitir huecos)
        var inventory = _currentHero.inventory;
        int totalCells = gridWidth * gridHeight;
        if (inventory.Count < totalCells)
        {
            // Rellenar con nulls si faltan posiciones
            while (inventory.Count < totalCells)
                inventory.Add(null);
        }

        for (int i = 0; i < _cellControllers.Count; i++)
        {
            InventoryItemCellController cell = _cellControllers[i];
            cell.SetCellIndex(i);
            if (inventory != null && inventory[i] != null)
            {
                var item = inventory[i];
                var itemData = InventoryUtils.GetItemData(item.itemId);
                cell.SetItem(item, itemData);
            }
            else
            {
                cell.Clear();
            }
        }
    }

    /// <summary>
    /// Obtiene los ítems del inventario según el filtro actual.
    /// </summary>
    /// <returns>Lista de ítems filtrados</returns>
    private List<InventoryItem> GetFilteredItems()
    {
        var allItems = InventoryService.GetAllItems();

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
    /// <param name="itemId">ID del ítem que cambió</param>
    /// <param name="quantity">Cantidad cambiada</param>
    private void OnItemChanged(string itemId, int quantity)
    {
        UpdateInventoryDisplay();
        UpdateMoneyDisplay();
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
        if (sourceCellIndex < 0 || sourceCellIndex >= _cellControllers.Count ||
            targetCellIndex < 0 || targetCellIndex >= _cellControllers.Count)
        {
            Debug.LogError($"[InventoryPanelController] Índices de celda inválidos: origen={sourceCellIndex}, destino={targetCellIndex}");
            return;
        }

        var inventory = _currentHero.inventory;
        int totalCells = gridWidth * gridHeight;
        if (inventory.Count < totalCells)
        {
            while (inventory.Count < totalCells)
                inventory.Add(null);
        }

        // Si ambas celdas están vacías, no hacer nada
        if (inventory[sourceCellIndex] == null && inventory[targetCellIndex] == null)
            return;

        // Intercambiar o mover ítem
        if (inventory[sourceCellIndex] != null)
        {
            var temp = inventory[sourceCellIndex];
            inventory[sourceCellIndex] = inventory[targetCellIndex];
            inventory[targetCellIndex] = temp;
        }
        // Actualizar visualización
        UpdateInventoryDisplay();
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
