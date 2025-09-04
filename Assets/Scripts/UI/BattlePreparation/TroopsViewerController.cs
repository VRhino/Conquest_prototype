using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controlador para el componente Troops_viewer en battle preparation.
/// Maneja la paginación de tropas seleccionadas y la gestión de loadouts.
/// Forma parte integral del BattlePreparationController.
/// </summary>
public class TroopsViewerController : MonoBehaviour
{
    #region UI References
    
    [Header("Navigation")]
    [SerializeField] private Button rightChevron;
    [SerializeField] private Button leftChevron;
    
    [Header("Containers")]
    [SerializeField] private GameObject troopsContainerPlaceholder;
    [SerializeField] private Transform troopsContainer;
    [SerializeField] private GameObject squadOptionPrefab; 
    
    [Header("Actions")]
    [SerializeField] private Button showLoadoutsButton;
    [SerializeField] private Button saveLoadoutButton;

    [Header("Display")]
    [SerializeField] private TextMeshProUGUI totalLeadershipText;

    #endregion

    #region Private Fields

    private const int TROOPS_PER_PAGE = 5;
    
    private List<string> _selectedSquadIds;
    private List<SquadOptionUI> _currentPageOptions;
    private int _currentPageIndex = 0;
    private int _totalPages = 0;
    private HeroData _currentHero;
    private LoadoutSaveData _activeLoadout;
    private int _totalLeadershipCost = 0;
    
    #endregion

    #region Events

    /// <summary>
    /// Se dispara cuando la selección de tropas cambia.
    /// </summary>
    public System.Action<List<string>> OnSelectionChanged;
    
    #endregion
    
    #region Unity Lifecycle
    
    void Awake()
    {
        _selectedSquadIds = new List<string>();
        _currentPageOptions = new List<SquadOptionUI>();
    }
    void Start()
    {
        InitializeFromActiveLoadout();
        SetupButtonListeners();
        ValidateComponents();
    }
    void OnDestroy()
    {
        ClearButtonListeners();
    }
    
    #endregion
    
    #region Initialization
    
    /// <summary>
    /// Inicializa el viewer cargando el loadout activo del héroe en PlayerSessionService.
    /// </summary>
    private void InitializeFromActiveLoadout()
    {
        _currentHero = PlayerSessionService.SelectedHero;
        if (_currentHero == null)
        {
            Debug.LogError("[TroopsViewerController] No hay héroe seleccionado en PlayerSessionService");
            return;
        }
        
        _activeLoadout = _currentHero.loadouts.Find(l => l.isActive);
        if (_activeLoadout == null)
        {
            Debug.LogWarning("[TroopsViewerController] No hay loadout activo para el héroe");
            _selectedSquadIds.Clear();
        }
        else
        {
            _selectedSquadIds = new List<string>(_activeLoadout.squadIDs);
        }
        
        UpdateTotalLeadershipDisplay();

        RecalculatePagination();
        RenderCurrentPage();
    }
    
    private void UpdateTotalLeadershipDisplay()
    {
        if (_currentHero == null) return;
        
         _totalLeadershipCost = 0;

        foreach (var squadId in _selectedSquadIds)
        {
            var squadData = SquadDataService.GetSquadById(squadId);
            if (squadData != null)
            {
                _totalLeadershipCost += squadData.leadershipCost;
            }
        }
        
        float availableLeadership = DataCacheService.getHeroLeadership(_currentHero.heroName);
        totalLeadershipText.text = $"{_totalLeadershipCost}/{(int)availableLeadership}";
    }
    
    /// <summary>
    /// Configura los listeners de los botones.
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

        if (showLoadoutsButton != null)
        {
            showLoadoutsButton.onClick.RemoveAllListeners();
            showLoadoutsButton.onClick.AddListener(OnShowLoadoutsClicked);
        }

        if (saveLoadoutButton != null)
        {
            saveLoadoutButton.onClick.RemoveAllListeners();
            saveLoadoutButton.onClick.AddListener(OnSaveLoadoutClicked);
        }
    }
    
    /// <summary>
    /// Limpia los listeners de los botones.
    /// </summary>
    private void ClearButtonListeners()
    {
        if (rightChevron != null) rightChevron.onClick.RemoveAllListeners();
        if (leftChevron != null) leftChevron.onClick.RemoveAllListeners();
        if (showLoadoutsButton != null) showLoadoutsButton.onClick.RemoveAllListeners();
        if (saveLoadoutButton != null) saveLoadoutButton.onClick.RemoveAllListeners();
    }
    
    #endregion
    
    #region Pagination
    
    /// <summary>
    /// Recalcula la paginación basado en la cantidad de tropas seleccionadas.
    /// </summary>
    private void RecalculatePagination()
    {
        _totalPages = Mathf.CeilToInt((float)_selectedSquadIds.Count / TROOPS_PER_PAGE);
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
    /// Renderiza la página actual destruyendo las opciones existentes y creando nuevas.
    /// </summary>
    private void RenderCurrentPage()
    {
        ClearTroopsContainer();

        if (_selectedSquadIds.Count == 0)
        {
            // No hay tropas seleccionadas, el placeholder permanece visible
            return;
        }

        // Calcular rango de squads para la página actual
        int startIndex = _currentPageIndex * TROOPS_PER_PAGE;
        int endIndex = Mathf.Min(startIndex + TROOPS_PER_PAGE, _selectedSquadIds.Count);
        // Instanciar Squad_Option para cada squad en la página
        for (int i = startIndex; i < endIndex; i++)
        {
            string squadId = _selectedSquadIds[i];
            CreateSquadOption(squadId);
        }
    }
    
    /// <summary>
    /// Crea una Squad_Option para un squad específico.
    /// </summary>
    /// <param name="squadId">ID del squad</param>
    private void CreateSquadOption(string squadId)
    {
        if (squadOptionPrefab == null || troopsContainer == null)
        {
            Debug.LogError("[TroopsViewerController] Prefab o container no asignados");
            return;
        }
        
        // Obtener datos del squad
        var squadData = SquadDataService.GetSquadById(squadId);
        if (squadData == null)
        {
            Debug.LogWarning($"[TroopsViewerController] No se encontró SquadData para ID: {squadId}");
            return;
        }
        _totalLeadershipCost += squadData.leadershipCost;
        // Buscar instancia del squad en el progreso del héroe
        var squadInstance = _currentHero?.squadProgress?.Find(s => s.baseSquadID == squadId);
        
        // Instanciar prefab
        GameObject optionGO = Instantiate(squadOptionPrefab, troopsContainer);
        SquadOptionUI optionUI = optionGO.GetComponent<SquadOptionUI>();
        
        if (optionUI != null)
        {
            // Configurar datos básicos
            optionUI.SetSquadData(squadData);
            optionUI.ToggleBattlePreMode();
            
            // Configurar datos de instancia si existen
            if (squadInstance != null)
            {
                optionUI.SetInstanceData(squadInstance.level.ToString(), squadInstance.unitsAlive);
            }
            
            // Configurar estado seleccionado
            optionUI.SetSelected(true); // Siempre seleccionado en este contexto
            
            // Configurar callback de click
            optionUI.onClick = () => OnSquadOptionClicked(squadId);
            
            _currentPageOptions.Add(optionUI);
        }
    }
    
    /// <summary>
    /// Limpia todas las Squad_Option del container.
    /// </summary>
    private void ClearTroopsContainer()
    {
        foreach (Transform child in troopsContainer)
        {
            Destroy(child.gameObject);
        }
        _currentPageOptions.Clear();
    }
    
    #endregion
    
    #region Squad Selection
    
    /// <summary>
    /// Maneja el click en una Squad_Option.
    /// </summary>
    /// <param name="squadId">ID del squad clickeado</param>
    private void OnSquadOptionClicked(string squadId)
    {
        // Toggle selección del squad
        if (_selectedSquadIds.Contains(squadId))
        {
            _selectedSquadIds.Remove(squadId);
            Debug.Log($"[TroopsViewerController] Squad removido de selección: {squadId}");
        }
        else
        {
            _selectedSquadIds.Add(squadId);
            Debug.Log($"[TroopsViewerController] Squad agregado a selección: {squadId}");
        }
        
        // Recalcular paginación si cambió el número de elementos
        RecalculatePagination();
        
        // Re-renderizar página actual
        RenderCurrentPage();
        
        // Notificar cambio
        OnSelectionChanged?.Invoke(new List<string>(_selectedSquadIds));
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
    
    /// <summary>
    /// Maneja el click en el botón Show Loadouts.
    /// </summary>
    private void OnShowLoadoutsClicked()
    {
        Debug.Log("[TroopsViewerController] Show loadouts clicked");
        // TODO: Implementar panel de loadouts
    }
    
    /// <summary>
    /// Maneja el click en el botón Save Loadout.
    /// </summary>
    private void OnSaveLoadoutClicked()
    {
        SaveCurrentLoadout();
    }
    
    #endregion
    
    #region Loadout Management
    
    /// <summary>
    /// Guarda la selección actual en el loadout activo.
    /// </summary>
    private void SaveCurrentLoadout()
    {
        if (_currentHero == null || _activeLoadout == null)
        {
            Debug.LogWarning("[TroopsViewerController] No hay héroe o loadout activo para guardar");
            return;
        }
        
        // Actualizar squadIDs del loadout activo
        _activeLoadout.squadIDs = new List<string>(_selectedSquadIds);
        
        // TODO: Calcular totalLeadership basado en los squads seleccionados
        // _activeLoadout.totalLeadership = CalculateTotalLeadership();
        
        // Guardar datos del jugador
        if (PlayerSessionService.CurrentPlayer != null)
        {
            SaveSystem.SavePlayer(PlayerSessionService.CurrentPlayer);
            Debug.Log($"[TroopsViewerController] Loadout '{_activeLoadout.name}' guardado con {_selectedSquadIds.Count} squads");
        }
        else
        {
            Debug.LogWarning("[TroopsViewerController] No hay CurrentPlayer para guardar");
        }
    }
    
    #endregion
    
    #region Public API
    
    /// <summary>
    /// Actualiza el viewer desde un loadout específico.
    /// </summary>
    /// <param name="loadout">Loadout con los squads a mostrar</param>
    public void UpdateFromLoadout(LoadoutSaveData loadout)
    {
        if (loadout == null)
        {
            _selectedSquadIds.Clear();
        }
        else
        {
            _selectedSquadIds = new List<string>(loadout.squadIDs);
            _activeLoadout = loadout;
        }
        
        RecalculatePagination();
        RenderCurrentPage();
    }
    
    /// <summary>
    /// Agrega un squad a la selección.
    /// </summary>
    /// <param name="squadId">ID del squad a agregar</param>
    public void AddSquadToSelection(string squadId)
    {
        if (!_selectedSquadIds.Contains(squadId))
        {
            _selectedSquadIds.Add(squadId);
            RecalculatePagination();
            RenderCurrentPage();
            OnSelectionChanged?.Invoke(new List<string>(_selectedSquadIds));
        }
    }
    
    /// <summary>
    /// Remueve un squad de la selección.
    /// </summary>
    /// <param name="squadId">ID del squad a remover</param>
    public void RemoveSquadFromSelection(string squadId)
    {
        if (_selectedSquadIds.Remove(squadId))
        {
            RecalculatePagination();
            RenderCurrentPage();
            OnSelectionChanged?.Invoke(new List<string>(_selectedSquadIds));
        }
    }
    
    /// <summary>
    /// Obtiene la selección actual de squads.
    /// </summary>
    /// <returns>Lista de IDs de squads seleccionados</returns>
    public List<string> GetCurrentSelection()
    {
        return new List<string>(_selectedSquadIds);
    }
    
    /// <summary>
    /// Recarga desde el loadout activo del héroe actual.
    /// </summary>
    public void RefreshFromActiveLoadout()
    {
        InitializeFromActiveLoadout();
    }
    
    #endregion
    
    #region Validation
    
    /// <summary>
    /// Valida que los componentes críticos estén asignados.
    /// </summary>
    private void ValidateComponents()
    {
        List<string> missingComponents = new List<string>();
        
        if (troopsContainer == null) missingComponents.Add("troopsContainer");
        if (squadOptionPrefab == null) missingComponents.Add("squadOptionPrefab");
        if (rightChevron == null) missingComponents.Add("rightChevron");
        if (leftChevron == null) missingComponents.Add("leftChevron");
        if (showLoadoutsButton == null) missingComponents.Add("showLoadoutsButton");
        if (saveLoadoutButton == null) missingComponents.Add("saveLoadoutButton");
        
        if (missingComponents.Count > 0)
        {
            Debug.LogWarning($"[TroopsViewerController] Componentes faltantes: {string.Join(", ", missingComponents)}");
        }
    }
    
    #endregion
}
