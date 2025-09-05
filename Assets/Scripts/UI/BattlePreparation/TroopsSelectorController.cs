using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controlador que permite al héroe seleccionar una squad instance de su squadProgress.
/// Utiliza TroopsViewerController como widget reutilizable con filtros por tipo de unidad.
/// Gestiona validación de liderazgo y evita duplicados en el loadout activo.
/// </summary>
public class TroopsSelectorController : MonoBehaviour
{
    #region UI References
    
    [Header("Widget")]
    [SerializeField] private TroopsViewerController troopsViewerWidget;
    
    [Header("Filters")]
    [SerializeField] private Button allFilterButton;
    [SerializeField] private Button infantryFilterButton;
    [SerializeField] private Button distanceFilterButton;
    [SerializeField] private Button cavalryFilterButton;
    
    [Header("Actions")]
    [SerializeField] private Button closeButton;
    
    #endregion

    #region Private Fields

    private HeroData _currentHero;
    private List<string> _SquadInstancesSelected;
    private float _maxLeadership;
    private float _currentLeadershipUsed;

    private List<string> _allAvailableSquadInstanceIDs = new List<string>();
    private List<string> _filteredSquadInstanceIDs = new List<string>();
    private UnitType? _currentFilter = null;
    
    #endregion

    #region Events

    /// <summary>
    /// Se dispara cuando se selecciona una squad instance.
    /// Parámetro: squadInstanceID seleccionado
    /// </summary>
    public System.Action<string> OnSquadSelected;
    
    /// <summary>
    /// Se dispara cuando se cierra el selector.
    /// </summary>
    public System.Action OnSelectorClosed;
    
    #endregion
    
    #region Unity Lifecycle
    
    void OnDestroy()
    {
        ClearButtonListeners();
        ClearWidgetListeners();
    }
    
    #endregion
    
    #region Initialization
    
    /// <summary>
    /// Inicializa la conexión con el widget.
    /// </summary>
    private void InitializeWidget()
    {
        if (troopsViewerWidget != null)
        {
            troopsViewerWidget.OnItemsRequested += OnItemsRequested;
            troopsViewerWidget.OnItemClicked += OnSquadInstanceClicked;
            troopsViewerWidget.initialize(8);
        }
    }
    
    /// <summary>
    /// Configura los listeners de los botones de filtros y acciones.
    /// </summary>
    private void SetupButtonListeners()
    {
        if (allFilterButton != null)
            allFilterButton.onClick.AddListener(() => ApplyFilter(null));
            
        if (infantryFilterButton != null)
            infantryFilterButton.onClick.AddListener(() => ApplyFilter(UnitType.Infantry));
            
        if (distanceFilterButton != null)
            distanceFilterButton.onClick.AddListener(() => ApplyFilter(UnitType.Distance));
            
        if (cavalryFilterButton != null)
            cavalryFilterButton.onClick.AddListener(() => ApplyFilter(UnitType.Cavalry));
            
        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);
    }
    
    /// <summary>
    /// Limpia los listeners de los botones.
    /// </summary>
    private void ClearButtonListeners()
    {
        if (allFilterButton != null)
            allFilterButton.onClick.RemoveAllListeners();
            
        if (infantryFilterButton != null)
            infantryFilterButton.onClick.RemoveAllListeners();
            
        if (distanceFilterButton != null)
            distanceFilterButton.onClick.RemoveAllListeners();
            
        if (cavalryFilterButton != null)
            cavalryFilterButton.onClick.RemoveAllListeners();
            
        if (closeButton != null)
            closeButton.onClick.RemoveAllListeners();
    }
    
    /// <summary>
    /// Limpia los listeners del widget.
    /// </summary>
    private void ClearWidgetListeners()
    {
        if (troopsViewerWidget != null)
        {
            troopsViewerWidget.OnItemsRequested -= OnItemsRequested;
            troopsViewerWidget.OnItemClicked -= OnSquadInstanceClicked;
        }
    }

    private void SetAllAvailableSquadInstanceIDs(List<string> list)
    {
        _allAvailableSquadInstanceIDs = new List<string>(list);
    }

    #endregion

    #region Public API

    /// <summary>
    /// Inicializa el selector con los datos del héroe y loadout activo.
    /// </summary>
    /// <param name="hero">Datos del héroe actual</param>
    /// <param name="SquadInstancesSelected">Loadout activo</param>
    /// <param name="maxLeadership">Liderazgo máximo disponible</param>
    public void Initialize(HeroData hero, List<string> SquadInstancesSelected, float maxLeadership)
    {
        _currentHero = hero;
        _SquadInstancesSelected = SquadInstancesSelected;
        _maxLeadership = maxLeadership;
        _allAvailableSquadInstanceIDs = new List<string>();
        _filteredSquadInstanceIDs = new List<string>();
        
        SetupButtonListeners();
        ValidateComponents();
        CalculateCurrentLeadershipUsage();
        BuildAvailableSquadsList();
        InitializeWidget();
        ApplyFilter(null); // Mostrar todos inicialmente
        UpdateFilterButtonStates();
    }
    
    /// <summary>
    /// Muestra el selector.
    /// </summary>
    public void Show()
    {
        gameObject.SetActive(true);
    }
    
    /// <summary>
    /// Oculta el selector y notifica el cierre.
    /// </summary>
    public void Hide()
    {
        _allAvailableSquadInstanceIDs = new List<string>();
        troopsViewerWidget?.SetItems(new List<string>(), "hide");
        ClearWidgetListeners();
        gameObject.SetActive(false);
        OnSelectorClosed?.Invoke();
    }
    
    #endregion
    
    #region Data Management
    
    /// <summary>
    /// Calcula el liderazgo actualmente en uso por el loadout activo.
    /// </summary>
    private void CalculateCurrentLeadershipUsage()
    {
        List<string> squadsIds = SquadDataService.convertInstanceIDsToBaseSquadIDs(_SquadInstancesSelected, _currentHero.squadProgress);

        _currentLeadershipUsed = squadsIds.Sum(s =>
        {
            var squadData = SquadDataService.GetSquadById(s);
            return squadData?.leadershipCost ?? 0f;
        });
    }
    
    /// <summary>
    /// Construye la lista de squad instances disponibles para seleccionar.
    /// Excluye las que ya están en el loadout activo.
    /// </summary>
    private void BuildAvailableSquadsList()
    {
        SetAllAvailableSquadInstanceIDs(new List<string>());

        if (_currentHero?.squadProgress == null) return;

        // Obtener squadInstanceIDs ya en el loadout activo
        var squadInstanceIDsInLoadout = _SquadInstancesSelected ?? new List<string>();
        
        // Filtrar squad instances del héroe que no estén ya en el loadout
        foreach (var squadInstance in _currentHero.squadProgress)
        {
            // Skip si ya está en el loadout activo
            if (squadInstanceIDsInLoadout.Contains(squadInstance.id))
                continue;
                
            // Solo incluir squads que tengan unidades vivas
            if (squadInstance.unitsAlive > 0)
            {
                _allAvailableSquadInstanceIDs.Add(squadInstance.id);
            }
        }
    }
    
    /// <summary>
    /// Aplica un filtro por tipo de unidad.
    /// </summary>
    /// <param name="filterType">Tipo de unidad a filtrar, null para mostrar todos</param>
    private void ApplyFilter(UnitType? filterType)
    {
        _currentFilter = filterType;
        _filteredSquadInstanceIDs = new List<string>();


        if (filterType == null)
        {
            // Mostrar todos
            _filteredSquadInstanceIDs.AddRange(_allAvailableSquadInstanceIDs);
        }
        else
        {
            // Filtrar por tipo
            foreach (var squadInstanceID in _allAvailableSquadInstanceIDs)
            {
                var squadInstance = GetSquadInstanceById(squadInstanceID);
                if (squadInstance != null)
                {
                    var squadData = SquadDataService.GetSquadById(squadInstance.baseSquadID);
                    if (squadData != null && squadData.unitType == filterType)
                    {
                        _filteredSquadInstanceIDs.Add(squadInstanceID);
                    }
                }
            }

        }

        // Actualizar el widget con la lista filtrada
        if (troopsViewerWidget != null)
        {
            bool successful = troopsViewerWidget.SetItems(new List<string>(_filteredSquadInstanceIDs), "ApplyFilter");
            if (!successful) Debug.LogError("[TroopsSelector] Failed to set items in TroopsViewerWidget");
        }
        
        UpdateFilterButtonStates();
    }
    
    /// <summary>
    /// Obtiene una squad instance por su ID.
    /// </summary>
    /// <param name="squadInstanceID">ID de la squad instance</param>
    /// <returns>SquadInstanceData o null si no se encuentra</returns>
    private SquadInstanceData GetSquadInstanceById(string squadInstanceID)
    {
        if (_currentHero?.squadProgress == null)
            return null;
            
        return _currentHero.squadProgress.FirstOrDefault(s => s.id == squadInstanceID);
    }
    
    /// <summary>
    /// Verifica si una squad instance puede ser seleccionada (no excede límite de liderazgo).
    /// </summary>
    /// <param name="squadInstanceID">ID de la squad instance</param>
    /// <returns>True si puede ser seleccionada</returns>
    private bool CanSelectSquadInstance(string squadInstanceID)
    {
        var squadInstance = GetSquadInstanceById(squadInstanceID);
        if (squadInstance == null)
            return false;
            
        var squadData = SquadDataService.GetSquadById(squadInstance.baseSquadID);
        if (squadData == null)
            return false;
            
        // Verificar si agregar esta squad excedería el límite de liderazgo
        float totalCostWithNewSquad = _currentLeadershipUsed + squadData.leadershipCost;
        return totalCostWithNewSquad <= _maxLeadership;
    }
    
    #endregion
    
    #region Widget Event Handlers
    
    /// <summary>
    /// Maneja cuando el widget solicita crear un item específico.
    /// </summary>
    /// <param name="squadInstanceID">ID de la squad instance</param>
    /// <param name="container">Container donde crear el item</param>
    /// <param name="prefab">Prefab a instanciar</param>
    /// <returns>GameObject creado</returns>
    private GameObject OnItemsRequested(string squadInstanceID, Transform container, GameObject prefab)
    {
        return CreateSquadOption(squadInstanceID, container, prefab);
    }
    
    /// <summary>
    /// Crea una Squad_Option para una squad instance específica.
    /// </summary>
    /// <param name="squadInstanceID">ID de la squad instance</param>
    /// <param name="container">Container donde crear la opción</param>
    /// <param name="prefab">Prefab a instanciar</param>
    /// <returns>GameObject creado</returns>
    private GameObject CreateSquadOption(string squadInstanceID, Transform container, GameObject prefab)
    {
        if (prefab == null || container == null)
        {
            Debug.LogError("CreateSquadOption: Prefab or container is null");
            return null;
        }
        
        var squadInstance = GetSquadInstanceById(squadInstanceID);
        if (squadInstance == null)
        {
            Debug.LogError($"CreateSquadOption: SquadInstance not found for ID: {squadInstanceID}");
            return null;
        }
        
        var squadData = SquadDataService.GetSquadById(squadInstance.baseSquadID);
        if (squadData == null)
        {
            Debug.LogError($"CreateSquadOption: SquadData not found for baseSquadID: {squadInstance.baseSquadID}");
            return null;
        }
        
        GameObject optionGO = Instantiate(prefab, container);
        SquadOptionUI optionUI = optionGO.GetComponent<SquadOptionUI>();
        if (optionUI != null)
        {
            // Configurar datos básicos del squad
            optionUI.SetSquadData(squadData);
            optionUI.ToggleBattlePreMode();
            
            // Configurar datos de instancia
            optionUI.SetInstanceData(squadInstance.level.ToString(), squadInstance.unitsAlive);
            
            // Configurar estado no seleccionado inicialmente
            optionUI.SetSelected(false);
            
            // Configurar estado habilitado/deshabilitado según liderazgo
            bool canSelect = CanSelectSquadInstance(squadInstanceID);
            
            // Configurar callback de click
            optionUI.onClick = () => {
                if (canSelect)
                {
                    troopsViewerWidget?.TriggerItemClick(squadInstance.id);
                }
            };
            
            // Si no puede seleccionarse, deshabilitar el botón
            if (!canSelect)
            {
                var button = optionUI.GetComponent<Button>();
                if (button != null)
                {
                    button.interactable = false;
                }
            }
        }
        
        return optionGO;
    }
    
    /// <summary>
    /// Maneja el click en una squad instance desde el widget.
    /// </summary>
    /// <param name="squadInstanceID">ID de la squad instance clickeada</param>
    private void OnSquadInstanceClicked(string squadInstanceID)
    {
        // Verificar si la squad puede ser seleccionada
        if (!CanSelectSquadInstance(squadInstanceID))
        {
            Debug.LogWarning($"Cannot select squad instance {squadInstanceID}: would exceed leadership limit");
            return;
        }
        
        // Notificar la selección y cerrar el selector
        OnSquadSelected?.Invoke(squadInstanceID);
        Hide();
    }
    
    #endregion
    
    #region UI State Management
    
    /// <summary>
    /// Actualiza el estado visual de los botones de filtro.
    /// </summary>
    private void UpdateFilterButtonStates()
    {
        // Actualizar estado activo/inactivo de botones de filtro
        UpdateFilterButton(allFilterButton, _currentFilter == null);
        UpdateFilterButton(infantryFilterButton, _currentFilter == UnitType.Infantry);
        UpdateFilterButton(distanceFilterButton, _currentFilter == UnitType.Distance);
        UpdateFilterButton(cavalryFilterButton, _currentFilter == UnitType.Cavalry);
        
        // Deshabilitar botones de filtro si no hay squad instances de ese tipo
        UpdateFilterButtonAvailability();
    }
    
    /// <summary>
    /// Actualiza el estado visual de un botón de filtro específico.
    /// </summary>
    /// <param name="button">Botón a actualizar</param>
    /// <param name="isActive">Si el filtro está activo</param>
    private void UpdateFilterButton(Button button, bool isActive)
    {
        if (button == null) return;
        
        Image outline = button.GetComponent<Image>();
        if (outline == null) return;
        // Cambiar el color o estado visual según si está activo
        var colors = button.colors;
        if (isActive)
        {
            outline.color = new Color32(217, 159, 87, 255);
        }
        else
        {
            outline.color = colors.normalColor;
        }
    }
    
    /// <summary>
    /// Actualiza la disponibilidad de los botones de filtro según si hay squads de cada tipo.
    /// </summary>
    private void UpdateFilterButtonAvailability()
    {
        if (infantryFilterButton != null)
            infantryFilterButton.interactable = HasSquadsOfType(UnitType.Infantry);
            
        if (distanceFilterButton != null)
            distanceFilterButton.interactable = HasSquadsOfType(UnitType.Distance);
            
        if (cavalryFilterButton != null)
            cavalryFilterButton.interactable = HasSquadsOfType(UnitType.Cavalry);
    }
    
    /// <summary>
    /// Verifica si hay squad instances disponibles de un tipo específico.
    /// </summary>
    /// <param name="unitType">Tipo de unidad a verificar</param>
    /// <returns>True si hay squads disponibles de ese tipo</returns>
    private bool HasSquadsOfType(UnitType unitType)
    {
        foreach (var squadInstanceID in _allAvailableSquadInstanceIDs)
        {
            var squadInstance = GetSquadInstanceById(squadInstanceID);
            if (squadInstance != null)
            {
                var squadData = SquadDataService.GetSquadById(squadInstance.baseSquadID);
                if (squadData != null && squadData.unitType == unitType)
                {
                    return true;
                }
            }
        }
        return false;
    }
    
    #endregion
    
    #region Validation
    
    /// <summary>
    /// Valida que los componentes críticos estén asignados.
    /// </summary>
    private void ValidateComponents()
    {
        List<string> missingComponents = new List<string>();
        
        if (troopsViewerWidget == null)
            missingComponents.Add("TroopsViewerWidget");
            
        if (allFilterButton == null)
            missingComponents.Add("AllFilterButton");
            
        if (missingComponents.Count > 0)
        {
            Debug.LogError($"TroopsSelectorController: Missing required components: {string.Join(", ", missingComponents)}");
        }
    }
    
    #endregion
}
