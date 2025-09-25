using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Widget reutilizable de paginación para listas de elementos.
/// Maneja la navegación entre páginas y la instanciación de elementos.
/// Completamente agnóstico del dominio - puede usarse para cualquier tipo de lista.
/// </summary>
public class TroopsViewerController : MonoBehaviour
{
    #region UI References

    private string identifier = "";
    
    [Header("Navigation")]
    [SerializeField] private Button rightChevron;
    [SerializeField] private Button leftChevron;
    
    [Header("Containers")]
    [SerializeField] private Transform itemContainerPlaceholder;
    [SerializeField] private Transform itemContainer;
    [SerializeField] private GameObject itemPrefab;
    [SerializeField] private GameObject itemPrefabPlaceHolder;

    #endregion

    #region Configuration
    
    [Header("Configuration")]
    [SerializeField] private int itemsPerPage = 5;
    
    #endregion

    #region Private Fields

    private List<string> _itemIds = new List<string>();
    private List<GameObject> _currentPageItems = new List<GameObject>();
    private int _currentPageIndex = 0;
    private int _totalPages = 0;
    private bool _isInitialized = false;

    #endregion

    #region Events

    /// <summary>
    /// Se dispara cuando se hace click en un item.
    /// </summary>
    private Action<string> OnItemClicked;
    
    /// <summary>
    /// Se dispara cuando el widget necesita crear un item específico.
    /// Parámetros: itemId, container, prefab
    /// Retorna: GameObject creado
    /// </summary>
    private Func<string, Transform, GameObject, GameObject> OnItemsRequested;
    
    /// <summary>
    /// Se dispara cuando se hace click en un placeholder.
    /// </summary>
    private Action OnPlaceholderClicked;

    public void ConnectExternalEvents(
        Action<string> onItemClicked,
        Func<string, Transform, GameObject, GameObject> onItemsRequested,
        Action onPlaceholderClicked
    )
    {
        OnItemClicked = onItemClicked;
        OnItemsRequested = onItemsRequested;
        if (onPlaceholderClicked != null) OnPlaceholderClicked = onPlaceholderClicked;
    }

    public void DisconnectAllEvents()
    {
        OnItemClicked = null;
        OnItemsRequested = null;
        OnPlaceholderClicked = null;
    }

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        identifier = Guid.NewGuid().ToString();
    }

    
    void OnDestroy()
    {
        ClearButtonListeners();
    }

    #endregion

    #region Initialization

    public void initialize(int itemPerPage = 5)
    {
        itemsPerPage = itemPerPage;
        SetupButtonListeners();
        ValidateComponents();
        RenderPlaceholder();
        _isInitialized = true;
    }
    
    /// <summary>
    /// Configura los listeners de los botones de navegación.
    /// </summary>
    private void SetupButtonListeners()
    {
        if (rightChevron != null)
        {
            rightChevron.onClick.RemoveAllListeners();
            rightChevron.onClick.AddListener(OnRightChevronClicked);
        }

        if (leftChevron != null)
        {
            leftChevron.onClick.RemoveAllListeners();
            leftChevron.onClick.AddListener(OnLeftChevronClicked);
        }
    }
    
    /// <summary>
    /// Limpia los listeners de los botones.
    /// </summary>
    private void ClearButtonListeners()
    {
        if (rightChevron != null) rightChevron.onClick.RemoveAllListeners();
        if (leftChevron != null) leftChevron.onClick.RemoveAllListeners();
    }
    
    #endregion
    
    #region Pagination
    
    /// <summary>
    /// Recalcula la paginación basado en la cantidad de items.
    /// </summary>
    private void RecalculatePagination()
    {
        _totalPages = Mathf.CeilToInt((float)_itemIds.Count / itemsPerPage);
        if (_totalPages == 0) _totalPages = 1; // Mínimo una página
        
        // Asegurar que el índice actual sea válido
        _currentPageIndex = Mathf.Clamp(_currentPageIndex, 0, _totalPages - 1);
        
        UpdateChevronStates();
    }
    
    /// <summary>
    /// Actualiza la visibilidad de los chevrons basado en la paginación.
    /// </summary>
    private void UpdateChevronStates()
    {
        if (leftChevron != null)
            leftChevron.gameObject.SetActive(_currentPageIndex > 0);
        
        if (rightChevron != null)
            rightChevron.gameObject.SetActive(_currentPageIndex < _totalPages - 1);
    }
    
    /// <summary>
    /// Navega a una página específica.
    /// </summary>
    /// <param name="pageIndex">Índice de la página (0-based)</param>
    private void NavigateToPage(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= _totalPages) return;
        
        _currentPageIndex = pageIndex;
        RenderCurrentPage();
        UpdateChevronStates();
    }

    #endregion

    #region Rendering

    /// <summary>
    /// Renderiza la página actual destruyendo los items existentes y creando nuevos.
    /// </summary>
    private void RenderCurrentPage()
    {
        ClearContainer();
        RenderPlaceholder();
        // Calcular rango de items para la página actual
        int startIndex = _currentPageIndex * itemsPerPage;
        int endIndex = Mathf.Min(startIndex + itemsPerPage, _itemIds.Count);

        // Crear items para la página
        for (int i = startIndex; i < endIndex; i++)
        {
            string itemId = _itemIds[i];
            CreateItem(itemId);
        }
    }
    /// <summary>
    /// Renderiza el placeholder
    /// </summary>
    private void RenderPlaceholder()
    {

        if (itemContainerPlaceholder != null)
            itemContainerPlaceholder.gameObject.SetActive(true);

        if (itemPrefabPlaceHolder != null && itemContainerPlaceholder != null)
        {
            int existingPlaceholders = itemContainerPlaceholder.childCount;
            if (existingPlaceholders == itemsPerPage) return;
            //llenar el placeholder container
            int itemsToCreate = itemsPerPage;
            for (int i = 0; i < itemsToCreate; i++)
            {
                GameObject placeholder = Instantiate(itemPrefabPlaceHolder, itemContainerPlaceholder);
                
                // Configurar click listener en cada placeholder
                Button placeholderButton = placeholder.GetComponent<Button>();
                if (placeholderButton != null)
                {
                    placeholderButton.onClick.AddListener(() => OnPlaceholderClicked?.Invoke());
                }
            }
        }
    }

    /// <summary>
    /// Crea un item específico solicitando al controlador superior.
    /// </summary>
    /// <param name="itemId">ID del item</param>
    private void CreateItem(string itemId)
    {
        if (itemPrefab == null || itemContainer == null)
        {
            Debug.LogError("[TroopsViewerController] Prefab o container no asignados");
            return;
        }

        // Solicitar al controlador superior que cree el item
        GameObject createdItem = OnItemsRequested?.Invoke(itemId, itemContainer, itemPrefab);

        if (createdItem != null) _currentPageItems.Add(createdItem);
    }
    
    /// <summary>
    /// Limpia todos los items del container.
    /// </summary>
    private void ClearContainer()
    {
        for (int i = 0; i < itemContainer.childCount; i++)
        {
            Transform child = itemContainer.GetChild(i);
            if (child != null)
                Destroy(child.gameObject);
        }
        _currentPageItems.Clear();
    }
    
    #endregion
    
    #region Button Handlers
    
    /// <summary>
    /// Maneja el click en el chevron derecho.
    /// </summary>
    private void OnRightChevronClicked()
    {
        NavigateToPage(_currentPageIndex + 1);
    }
    
    /// <summary>
    /// Maneja el click en el chevron izquierdo.
    /// </summary>
    private void OnLeftChevronClicked()
    {
        NavigateToPage(_currentPageIndex - 1);
    }
    
    #endregion
    
    #region Public API
    
    /// <summary>
    /// Establece la lista de items a mostrar.
    /// </summary>
    /// <param name="itemIds">Lista de IDs de items</param>
    public bool SetItems(List<string> itemIds, string whoCalled)
    {
        if (!_isInitialized) return false;

        _currentPageItems = new List<GameObject>();
        _itemIds = itemIds;
        _currentPageIndex = 0;
        RecalculatePagination();
        RenderCurrentPage();
        return true;
    }

    /// <summary>
    /// Obtiene el índice de la página actual.
    /// </summary>
    /// <returns>Índice de página actual (0-based)</returns>
    public int GetCurrentPageIndex()
    {
        return _currentPageIndex;
    }
    
    /// <summary>
    /// Obtiene el total de páginas.
    /// </summary>
    /// <returns>Total de páginas</returns>
    public int GetTotalPages()
    {
        return _totalPages;
    }
    
    /// <summary>
    /// Dispara el evento de click en un item (para ser usado por items creados).
    /// </summary>
    /// <param name="itemId">ID del item clickeado</param>
    public void TriggerItemClick(string itemId)
    {
        OnItemClicked?.Invoke(itemId);
    }
    
    #endregion
    
    #region Validation
    
    /// <summary>
    /// Valida que los componentes críticos estén asignados.
    /// </summary>
    private void ValidateComponents()
    {
        List<string> missingComponents = new List<string>();
        
        if (itemContainer == null) missingComponents.Add("itemContainer");
        if (itemPrefab == null) missingComponents.Add("itemPrefab");
        if (rightChevron == null) missingComponents.Add("rightChevron");
        if (leftChevron == null) missingComponents.Add("leftChevron");
        
        if (missingComponents.Count > 0)
        {
            Debug.LogWarning($"[TroopsViewerController] Componentes faltantes: {string.Join(", ", missingComponents)}");
        }
    }
    
    #endregion
}
