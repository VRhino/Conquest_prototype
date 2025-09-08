using System;
using System.Collections.Generic;
using BattleDrakeStudios.ModularCharacters;
using Data.Items;
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
        //solo para tensting
        initTestHero();

        initializeLocalHero();
        _equipmentPanel.InitializePanel();
        _equipmentPanel.PopulateFromSelectedHero();
        _equipmentPanel.SetEvents(OnEquipmentSlotClicked, null);
        if (!_itemPopUpSelector.IsInitialized)
        {
            _itemPopUpSelector.Initialize();
            _itemPopUpSelector.SetEvents(OnItemClicked);
        }
    }

    private void initTestHero()
    {
        LoadSystem.LoadDataForTesting(out HeroData localHero, out PlayerData player);
        PlayerSessionService.SetPlayer(player);
        PlayerSessionService.SetSelectedHero(localHero);
    }

    void OnDestroy()
    {
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

        // Si existe, actualizar
        if (_heroSliceMap.TryGetValue(heroData.heroName, out HeroSliceController existingController))
        {
            existingController.Initialize(heroData);
            Debug.Log($"[BattlePreparationController] Héroe actualizado: {heroData.heroName}");
            return true;
        }
        else
        {
            // Si no existe, crear
            return AddHero(heroData);
        }
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
    /// Obtiene el controlador de un héroe específico.
    /// </summary>
    /// <param name="heroName">Nombre del héroe</param>
    /// <returns>HeroSliceController o null si no existe</returns>
    public HeroSliceController GetHeroController(string heroName)
    {
        if (_heroSliceMap.TryGetValue(heroName, out HeroSliceController controller))
        {
            return controller;
        }
        return null;
    }

    /// <summary>
    /// Obtiene todos los héroes actualmente mostrados.
    /// </summary>
    /// <returns>Lista de nombres de héroes</returns>
    public List<string> GetAllHeroes()
    {
        return new List<string>(_heroSliceMap.Keys);
    }

    /// <summary>
    /// Limpia todos los héroes del battle preparation.
    /// </summary>
    public void ClearAllHeroes()
    {
        ClearAllHeroSlices();
        Debug.Log("[BattlePreparationController] Todos los héroes limpiados");
    }

    #endregion

    #region Private Methods

    private void initializeLocalHero()
    {
        HeroData localHero = PlayerSessionService.SelectedHero;
        if (localHero == null || localHero?.loadouts == null || localHero.loadouts.Count == 0)
        {
            Debug.LogError("[BattlePreparationController] No hay héroe seleccionado en PlayerSessionService o no tiene loadouts");
            return;
        }

        List<SquadIconData> squads = SquadDataService.ConvertLoadoutToSquadIconData(localHero.loadouts[0], localHero);
        HeroClassDefinition heroClass = HeroClassManager.GetClassDefinition(localHero.classId);

        HeroSliceData localHeroData = new HeroSliceData(
            heroIcon: null,
            classIcon: heroClass?.icon,
            heroName: localHero?.heroName,
            heroLevel: localHero?.level ?? 1,
            houseIcon: null,
            selectedSquads: squads ?? new List<SquadIconData>()
        );
        InventoryStorageService.Initialize(localHero);
        InventoryManager.Initialize(localHero);
        AddHero(localHeroData);
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
        if(item == null || itemData == null) Debug.LogWarning("[BattlePreparationController] Item o ItemData nulo en OnItemClicked");
        //equipar el item clicado
        bool success = InventoryManager.EquipItem(item);

        if (success) {
            _equipmentPanel.PopulateFromSelectedHero();
            _itemPopUpSelector.Hide();
        }
        else
            Debug.LogWarning($"[InventoryItemCellInteraction] No se pudo equipar: {itemData.name}");
    }

    #endregion
}
