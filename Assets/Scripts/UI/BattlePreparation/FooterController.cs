using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controlador superior que maneja la lógica específica de battle preparation.
/// Utiliza el TroopsViewerController como widget de paginación reutilizable.
/// Gestiona loadouts, datos de héroe y lógica de negocio específica.
/// </summary>
public class FooterController : MonoBehaviour
{
    #region UI References
    
    [Header("Widget")]
    [SerializeField] private TroopsViewerController troopsViewerWidget;
    
    [Header("Actions")]
    [SerializeField] private Button showLoadoutsButton;
    [SerializeField] private Button saveLoadoutButton;

    [Header("Display")]
    [SerializeField] private TextMeshProUGUI totalLeadershipText;
    
    [Header("Troop Selector")]
    [SerializeField] private TroopsSelectorController troopsSelectorController;
    [SerializeField] private GameObject SquadSelectionPanel;

    #endregion

    #region Private Fields

    private List<string> _heroSquadInstanceIDs;
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
        _heroSquadInstanceIDs = new List<string>();
    }
    
    void Start()
    {
        InitializeWidget();
        InitializeFromActiveLoadout();
        SetupButtonListeners();
        ValidateComponents();
    }
    
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
            troopsViewerWidget.OnItemClicked += OnSquadOptionClicked;
            troopsViewerWidget.OnItemsRequested += OnItemsRequested;
            troopsViewerWidget.OnPlaceholderClicked += OnPlaceholderClicked;
            troopsViewerWidget.initialize(5);
        }
        
        if (troopsSelectorController != null)
        {
            troopsSelectorController.OnSquadSelected += OnSquadSelectedFromSelector;
            troopsSelectorController.OnSelectorClosed += OnSelectorClosed;
        }
    }
    
    /// <summary>
    /// Inicializa cargando el loadout activo del héroe en PlayerSessionService.
    /// </summary>
    private void InitializeFromActiveLoadout()
    {
        _currentHero = PlayerSessionService.SelectedHero;
        if (_currentHero == null)
        {
            Debug.LogError("[FooterController] No hay héroe seleccionado en PlayerSessionService");
            return;
        }
        
        _activeLoadout = _currentHero.loadouts.Find(l => l.isActive);
        if (_activeLoadout == null)
        {
            Debug.LogWarning("[FooterController] No hay loadout activo para el héroe");
            _heroSquadInstanceIDs.Clear();
        }
        else
        {
            _heroSquadInstanceIDs = new List<string>(_activeLoadout.squadInstanceIDs);
        }
        
        UpdateTotalLeadershipDisplay();
        UpdateTroopsWidget();
    }
    
    /// <summary>
    /// Actualiza el widget con los datos actuales.
    /// </summary>
    private void UpdateTroopsWidget()
    {
        if (troopsViewerWidget != null)
        {
            bool successful = troopsViewerWidget.SetItems(new List<string>(_heroSquadInstanceIDs), "UpdateTroopsWidget");
            if (!successful) Debug.LogError("[FooterController] Failed to set items in TroopsViewerWidget");
        }
    }
    
    /// <summary>
    /// Calcula y actualiza el display de liderazgo total.
    /// </summary>
    private void UpdateTotalLeadershipDisplay()
    {
        if (_currentHero == null) return;
        
        _totalLeadershipCost = 0;

        List<string> squadIDs = SquadDataService.convertInstanceIDsToBaseSquadIDs(_heroSquadInstanceIDs, _currentHero.squadProgress);

        foreach (var squadId in squadIDs)
        {
            var squadData = SquadDataService.GetSquadById(squadId);
            if (squadData != null)
            {
                _totalLeadershipCost += squadData.leadershipCost;
            }
        }
        
        float availableLeadership = DataCacheService.getHeroLeadership(_currentHero.heroName);
        if (totalLeadershipText != null)
        {
            totalLeadershipText.text = $"{_totalLeadershipCost}/{(int)availableLeadership}";
        }
    }
    
    /// <summary>
    /// Configura los listeners de los botones específicos del footer.
    /// </summary>
    private void SetupButtonListeners()
    {
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
        if (showLoadoutsButton != null) showLoadoutsButton.onClick.RemoveAllListeners();
        if (saveLoadoutButton != null) saveLoadoutButton.onClick.RemoveAllListeners();
    }
    
    /// <summary>
    /// Limpia los listeners del widget.
    /// </summary>
    private void ClearWidgetListeners()
    {
        if (troopsViewerWidget != null)
        {
            troopsViewerWidget.OnItemClicked -= OnSquadOptionClicked;
            troopsViewerWidget.OnItemsRequested -= OnItemsRequested;
            troopsViewerWidget.OnPlaceholderClicked -= OnPlaceholderClicked;
        }
        
        if (troopsSelectorController != null)
        {
            troopsSelectorController.OnSquadSelected -= OnSquadSelectedFromSelector;
            troopsSelectorController.OnSelectorClosed -= OnSelectorClosed;
        }
    }
    
    #endregion
    
    #region Widget Event Handlers
    
    /// <summary>
    /// Maneja cuando el widget solicita crear un item específico.
    /// </summary>
    /// <param name="itemId">ID del item a crear</param>
    /// <param name="container">Container donde crear el item</param>
    /// <param name="prefab">Prefab a instanciar</param>
    /// <returns>GameObject creado</returns>
    private GameObject OnItemsRequested(string itemId, Transform container, GameObject prefab)
    {
        return CreateSquadOption(itemId, container, prefab);
    }
    
    /// <summary>
    /// Crea una Squad_Option para un squad específico.
    /// </summary>
    /// <param name="squadInstanceId">ID de la instancia del squad del héroe</param>
    /// <param name="container">Container donde crear la opción</param>
    /// <param name="prefab">Prefab a instanciar</param>
    /// <returns>GameObject creado</returns>
    private GameObject CreateSquadOption(string squadInstanceId, Transform container, GameObject prefab)
    {
        if (prefab == null || container == null)
        {
            Debug.LogError("[FooterController] Prefab o container no asignados");
            return null;
        }
        
        // Obtener la instancia del squad del héroe
        if (_currentHero == null)
        {
            Debug.LogError("[FooterController] No hay héroe seleccionado");
            return null;
        }

        SquadInstanceData squadInstance = _currentHero.squadProgress?.Find(s => s.id == squadInstanceId);
        if (squadInstance == null) return null;

        SquadData squadData = SquadDataService.GetSquadById(squadInstance.baseSquadID);
        if (squadData == null) return null;
        
        // Instanciar prefab
        GameObject optionGO = Instantiate(prefab, container);
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
            
            // Configurar callback de click - será manejado por el widget
            optionUI.onClick = () => troopsViewerWidget?.TriggerItemClick(squadInstance.id);
        }
        
        return optionGO;
    }
    
    /// <summary>
    /// Maneja el click en una Squad_Option desde el widget.
    /// </summary>
    /// <param name="squadId">ID del squad clickeado</param>
    private void OnSquadOptionClicked(string squadId)
    {
        if (_heroSquadInstanceIDs.Contains(squadId))  _heroSquadInstanceIDs.Remove(squadId);
        
        // Actualizar displays
        UpdateTotalLeadershipDisplay();
        UpdateTroopsWidget();
        
        // Notificar cambio
        BattlePreparationEvents.TriggerSquadsUpdated(_currentHero.heroName, SquadDataService.ConvertToSquadIconDataList(SquadDataService.convertInstanceIDsToBaseSquadIDs(_heroSquadInstanceIDs, _currentHero.squadProgress)));
    }
    
    /// <summary>
    /// Maneja el click en un placeholder desde el widget.
    /// </summary>
    private void OnPlaceholderClicked()
    {
        if (troopsSelectorController != null)
        {
            // Calcular liderazgo máximo disponible
            float maxLeadership = DataCacheService.getHeroLeadership(_currentHero.heroName);

            // Inicializar el selector con los datos actuales
            troopsSelectorController.Initialize(_currentHero, _heroSquadInstanceIDs, maxLeadership);
            // Mostrar el selector
            troopsSelectorController.Show();
            SquadSelectionPanel.SetActive(true);
        }
        else
        {
            Debug.LogError("[FooterController] TroopsSelectorController no asignado");
        }
    }
    
    #endregion
    
    #region Button Handlers
    
    /// <summary>
    /// Maneja el click en el botón Show Loadouts.
    /// </summary>
    private void OnShowLoadoutsClicked()
    {
        Debug.Log("[FooterController] Show loadouts clicked");
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
            Debug.LogWarning("[FooterController] No hay héroe o loadout activo para guardar");
            return;
        }
        
        // Actualizar squadIDs del loadout activo
        _activeLoadout.squadInstanceIDs = new List<string>(_heroSquadInstanceIDs);
        
        // TODO: Calcular totalLeadership basado en los squads seleccionados
        // _activeLoadout.totalLeadership = CalculateTotalLeadership();
        
        // Guardar datos del jugador
        if (PlayerSessionService.CurrentPlayer != null)
        {
            SaveSystem.SavePlayer(PlayerSessionService.CurrentPlayer);
        }
        else
        {
            Debug.LogWarning("[FooterController] No hay CurrentPlayer para guardar");
        }
    }
    
    #endregion

    #region TroopsSelectorController Integration

    /// <summary>
    /// Maneja cuando se selecciona una squad desde el TroopsSelectorController.
    /// </summary>
    /// <param name="squadInstanceID">ID de la squad instance seleccionada</param>
    private void OnSquadSelectedFromSelector(string squadInstanceID)
    {
        if (!string.IsNullOrEmpty(squadInstanceID))
        {
            // Agregar la squad al loadout activo
            _heroSquadInstanceIDs.Add(squadInstanceID);
            // Actualizar displays
            UpdateTotalLeadershipDisplay();
            UpdateTroopsWidget();

            List<SquadIconData> selectedSquads = SquadDataService.ConvertToSquadIconDataList(SquadDataService.convertInstanceIDsToBaseSquadIDs(_heroSquadInstanceIDs, _currentHero.squadProgress));
            BattlePreparationEvents.TriggerSquadsUpdated(_currentHero.heroName, selectedSquads);
        }
    }
    
    /// <summary>
    /// Maneja cuando se cierra el TroopsSelectorController.
    /// </summary>
    private void OnSelectorClosed()
    {
        SquadSelectionPanel.SetActive(false);
    }
    
    #endregion

    #region Validation

    /// <summary>
    /// Valida que los componentes críticos estén asignados.
    /// </summary>
    private void ValidateComponents()
    {
        List<string> missingComponents = new List<string>();
        
        if (troopsViewerWidget == null) missingComponents.Add("troopsViewerWidget");
        if (showLoadoutsButton == null) missingComponents.Add("showLoadoutsButton");
        if (saveLoadoutButton == null) missingComponents.Add("saveLoadoutButton");
        if (totalLeadershipText == null) missingComponents.Add("totalLeadershipText");
        
        if (missingComponents.Count > 0)
        {
            Debug.LogWarning($"[FooterController] Componentes faltantes: {string.Join(", ", missingComponents)}");
        }
    }
    
    #endregion
}
