using System;
using System.Collections;
using System.Collections.Generic;
using BattleDrakeStudios.ModularCharacters;
using Data.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controlador padre que maneja una lista de HeroSliceController.
/// Gestiona la creación, actualización y eliminación de slices de héroes.
/// </summary>
public class BattlePreparationController : MonoBehaviour
{
    #region UI References

    [Header("Hero Slice Management")]
    [SerializeField] public Transform heroSliceContainer;
    [SerializeField] public GameObject heroSlicePrefab;

    [Header("Equipment Management")]
    [SerializeField] private HeroEquipmentPanel _equipmentPanel;
    [SerializeField] private ItemSelectorController _itemPopUpSelector;
    [Header("Header Display")]
    [SerializeField] private TextMeshProUGUI timerDisplay;
    [SerializeField] private TextMeshProUGUI mapName;
    [Header("Timer Management")]
    [SerializeField] private TimerController _timerController;
    private BattleData _currentBattleData;

    #endregion

    #region Private Fields

    private List<HeroSliceController> _heroSliceControllers;
    private Dictionary<string, HeroSliceController> _heroSliceMap; // heroName -> controller

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        _heroSliceControllers = new List<HeroSliceController>();
        _heroSliceMap = new Dictionary<string, HeroSliceController>();

        // Usar datos de transición si existen
        _currentBattleData = BattleTransitionData.Instance.GetAndClearBattleData();
        if (_currentBattleData != null) Debug.Log($"[BattlePreparationController] Using transition data: {_currentBattleData.battleID}");
        else
        {
            // [TESTING ONLY] Setup test environment if available
            TestEnvironmentInitializer testEnv = FindAnyObjectByType<TestEnvironmentInitializer>();
            if (testEnv != null)
            {
                testEnv.SetupTestEnvironment();
                _currentBattleData = testEnv.GenerateBattleData(PlayerSessionService.SelectedHero);
                testEnv.SetGamePhase(GamePhase.BattlePreparation);
            }
        }

        InitializeTimerController();
        initializeBattlePreparation();
        _equipmentPanel.InitializePanel();
        _equipmentPanel.PopulateFromSelectedHero();
        _equipmentPanel.SetEvents(OnEquipmentSlotClicked, null);
        TooltipManager tooltipManager = FindAnyObjectByType<TooltipManager>();
        if (tooltipManager != null) _equipmentPanel.SetTooltipManager(tooltipManager);
        if (!_itemPopUpSelector.IsInitialized)
        {
            if (tooltipManager != null) _itemPopUpSelector.SetTooltipManager(tooltipManager);
            _itemPopUpSelector.Initialize();
            _itemPopUpSelector.SetEvents(OnItemClicked);
        }
    }

    void OnDestroy()
    {
        if (_timerController != null) _timerController.OnTimerFinished -= OnPreparationTimerFinished;

        ClearAllHeroSlices();
    }

    #endregion

    #region Public API

    /// <summary>
    /// Agrega un nuevo héroe al battle preparation.
    /// Crea un HeroSliceController y lo inicializa con los datos proporcionados.
    /// </summary>
    /// <param name="heroData">Datos del héroe a agregar</param>
    /// <returns>True si se agregó exitosamente, false si ya existe</returns>
    public bool AddHero(HeroSliceData heroData)
    {
        if (heroData == null || string.IsNullOrEmpty(heroData.heroName))
        {
            Debug.LogWarning("[BattlePreparationController] HeroData inválido");
            return false;
        }

        // Verificar si ya existe
        if (_heroSliceMap.ContainsKey(heroData.heroName))
        {
            Debug.LogWarning($"[BattlePreparationController] Héroe ya existe: {heroData.heroName}");
            return false;
        }

        // Crear nuevo HeroSlice
        HeroSliceController sliceController = CreateHeroSlice(heroData);
        if (sliceController != null)
        {
            _heroSliceControllers.Add(sliceController);
            _heroSliceMap[heroData.heroName] = sliceController;

            Debug.Log($"[BattlePreparationController] Héroe agregado: {heroData.heroName}");
            return true;
        }

        return false;
    }
    /// <summary>
    /// Actualiza la información de un héroe existente.
    /// Si el héroe no existe, lo crea.
    /// </summary>
    /// <param name="heroData">Datos actualizados del héroe</param>
    /// <returns>True si se actualizó/creó exitosamente</returns>
    public bool UpdateHero(HeroSliceData heroData)
    {
        if (heroData == null || string.IsNullOrEmpty(heroData.heroName))
        {
            Debug.LogWarning("[BattlePreparationController] HeroData inválido para actualización");
            return false;
        }

        if (_heroSliceMap.TryGetValue(heroData.heroName, out HeroSliceController existingController))
        {
            existingController.Initialize(heroData);
            Debug.Log($"[BattlePreparationController] Héroe actualizado: {heroData.heroName}");
            return true;
        }
        else return AddHero(heroData);
    }

    /// <summary>
    /// Remueve un héroe del battle preparation.
    /// </summary>
    /// <param name="heroName">Nombre del héroe a remover</param>
    /// <returns>True si se removió exitosamente</returns>
    public bool RemoveHero(string heroName)
    {
        if (string.IsNullOrEmpty(heroName))
        {
            Debug.LogWarning("[BattlePreparationController] Nombre de héroe inválido para remover");
            return false;
        }

        if (_heroSliceMap.TryGetValue(heroName, out HeroSliceController controller))
        {
            // Remover de las listas
            _heroSliceControllers.Remove(controller);
            _heroSliceMap.Remove(heroName);

            // Destruir el GameObject
            Destroy(controller.gameObject);

            Debug.Log($"[BattlePreparationController] Héroe removido: {heroName}");
            return true;
        }

        Debug.LogWarning($"[BattlePreparationController] Héroe no encontrado para remover: {heroName}");
        return false;
    }

    /// <summary>
    /// Limpia todos los héroes del battle preparation.
    /// </summary>
    public void ClearAllHeroes() { ClearAllHeroSlices(); }

    /// <summary>
    /// Obtiene acceso al TimerController para operaciones avanzadas.
    /// </summary>
    public TimerController TimerController => _timerController;
    #endregion

    #region Private Methods

    private void InitializeTimerController()
    {
        if (_timerController == null)
        {
            // Buscar en escena o crear instancia
            _timerController = FindAnyObjectByType<TimerController>();
            if (_timerController == null)
            {
                GameObject timerGO = new GameObject("TimerController");
                timerGO.transform.SetParent(transform);
                _timerController = timerGO.AddComponent<TimerController>();
            }
        }

        _timerController.Initialize(_currentBattleData.PreparationTimer, timerDisplay);
        _timerController.OnTimerFinished += OnPreparationTimerFinished;
    }

    private void OnPreparationTimerFinished()
    {
        Debug.Log("[BattlePreparationController] El tiempo de preparación ha terminado.");
        // Aquí podrías disparar un evento o llamar a un método para iniciar la batalla.
    }

    private void initializeBattlePreparation()
    {
        if (_currentBattleData == null)
        {
            Debug.LogError("[BattlePreparationController] No hay datos de batalla actuales para inicializar");
            return;
        }

        mapName.text = _currentBattleData.mapName;

        ClearAllHeroes();

        //identificar local hero en listas
        HeroData localHero = PlayerSessionService.SelectedHero;
        List<BattleHeroData> players = new List<BattleHeroData>();

        BattleHeroData localBattleHeroData = _currentBattleData.attackers.Find(bh => bh.heroName == localHero.heroName);
        if (localBattleHeroData != null) players = new List<BattleHeroData>(_currentBattleData.attackers);

        localBattleHeroData = _currentBattleData.defenders.Find(bh => bh.heroName == localHero.heroName);
        if (localBattleHeroData != null) players = new List<BattleHeroData>(_currentBattleData.defenders);

        if (players.Count == 0)
        {
            Debug.LogError("[BattlePreparationController] No se encontró el héroe local en los datos de batalla");
            return;
        }

        initializeLocalHero(localHero);

        foreach (var battleHero in players)
        {
            List<string> squadIDs = SquadDataService.getBaseSquadIDsFromInstances(battleHero.squadInstances);
            List<SquadIconData> squads = SquadDataService.ConvertToSquadIconDataList(squadIDs);
            HeroClassDefinition heroClass = HeroClassManager.GetClassDefinition(battleHero.classID);

            HeroSliceData heroData = new HeroSliceData(
                heroIcon: null,
                classIcon: heroClass?.icon,
                heroName: battleHero.heroName,
                heroLevel: battleHero.level,
                houseIcon: null,
                selectedSquads: squads ?? new List<SquadIconData>()
            );

            AddHero(heroData);
        }
        Debug.Log($"[BattlePreparationController] Héroes inicializados: {_heroSliceControllers.Count}");
        _timerController.SetCountDownSecs(_currentBattleData.PreparationTimer);
    }

    private void initializeLocalHero(HeroData localHero)
    {
        InventoryStorageService.Initialize(localHero);
        InventoryManager.Initialize(localHero);
    }


    /// <summary>
    /// Crea un nuevo HeroSliceController con los datos proporcionados.
    /// </summary>
    /// <param name="heroData">Datos del héroe</param>
    /// <returns>HeroSliceController creado o null si falla</returns>
    private HeroSliceController CreateHeroSlice(HeroSliceData heroData)
    {
        if (heroSliceContainer == null || heroSlicePrefab == null)
        {
            Debug.LogError("[BattlePreparationController] Container o prefab no asignados");
            return null;
        }

        // Instanciar prefab
        GameObject sliceGO = Instantiate(heroSlicePrefab, heroSliceContainer);
        HeroSliceController sliceController = sliceGO.GetComponent<HeroSliceController>();

        if (sliceController == null)
        {
            Debug.LogError("[BattlePreparationController] HeroSlice prefab no tiene HeroSliceController component");
            Destroy(sliceGO);
            return null;
        }

        // Inicializar con datos
        sliceController.Initialize(heroData);
        return sliceController;
    }

    /// <summary>
    /// Limpia todos los HeroSliceControllers existentes.
    /// </summary>
    private void ClearAllHeroSlices()
    {
        foreach (var controller in _heroSliceControllers)
        {
            if (controller != null && controller.gameObject != null)
            {
                Destroy(controller.gameObject);
            }
        }

        _heroSliceControllers.Clear();
        _heroSliceMap.Clear();
    }


    #endregion

    #region Event Handlers

    private void OnEquipmentSlotClicked(InventoryItem item, ItemData itemData, HeroEquipmentSlotController slotController)
    {
        if (slotController == null) return;

        List<InventoryItem> items = InventoryStorageService.GetItemsByTypeAndCategory(slotController.SlotType, slotController.SlotCategory);
        if (items == null || items.Count == 0)
        {
            Debug.LogWarning($"[BattlePreparationController] No hay items disponibles para el tipo y categoría del item: {itemData.name}");
            return;
        }
        RectTransform slotRect = _equipmentPanel.GetSlotTransform(slotController.SlotType, slotController.SlotCategory);
        _itemPopUpSelector.SetItems(items);
        _itemPopUpSelector.ShowNearElement(slotRect);
    }

    private void OnItemClicked(InventoryItem item, ItemData itemData)
    {
        if (item == null || itemData == null) Debug.LogWarning("[BattlePreparationController] Item o ItemData nulo en OnItemClicked");
        //equipar el item clicado
        bool success = InventoryManager.EquipItem(item);

        if (success)
        {
            _equipmentPanel.PopulateFromSelectedHero();
            _itemPopUpSelector.Hide();
        }
        else
            Debug.LogWarning($"[InventoryItemCellInteraction] No se pudo equipar: {itemData.name}");
    }

    #endregion
}
