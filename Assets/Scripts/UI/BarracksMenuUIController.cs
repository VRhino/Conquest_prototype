using UnityEngine;

using UnityEngine.UI;

using System;
using System.Linq;
public class BarracksMenuUIController : MonoBehaviour
{
    [Header("Listas de unidades por tipo")]
    public Transform infantryListContainer;
    public Transform cavalryListContainer;
    public Transform distanceListContainer;
    public GameObject squadListItemPrefab;
    [Header("Panel de selección de escuadrón")]
    public SquadSelectionPanel squadSelectionPanel;
    // Puedes asignar referencias a paneles, textos, botones, etc. desde el editor
    [Header("Panel principal del menú de barracas")]
    public GameObject mainPanel;

    [Header("Botones de acción")]
    public Button exitButton;
    public Button addInfantryButton;
    public Button addCavalryButton;
    public Button addDistanceButton;

    [Header("Textos de información")]
    public TMPro.TextMeshProUGUI expText;
    public TMPro.TextMeshProUGUI barracksSlotsText;


    private HeroData _currentHeroData;

    // Lógica para abrir el menú con la info del héroe
    public void Open(HeroData heroData)
    {
        _currentHeroData = heroData;
        if (mainPanel != null)
            mainPanel.SetActive(true);

        // Asignar listeners (solo una vez)
        if (exitButton != null)
        {
            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(Close);
        }
        if (addInfantryButton != null)
        {
            addInfantryButton.onClick.RemoveAllListeners();
            addInfantryButton.onClick.AddListener(() => OnAddUnitClicked(UnitType.Infantry));
        }
        if (addCavalryButton != null)
        {
            addCavalryButton.onClick.RemoveAllListeners();
            addCavalryButton.onClick.AddListener(() => OnAddUnitClicked(UnitType.Cavalry));
        }
        if (addDistanceButton != null)
        {
            addDistanceButton.onClick.RemoveAllListeners();
            addDistanceButton.onClick.AddListener(() => OnAddUnitClicked(UnitType.Distance));
        }

        // Limpiar listas visuales
        if (infantryListContainer != null)
            foreach (Transform c in infantryListContainer) Destroy(c.gameObject);
        if (cavalryListContainer != null)
            foreach (Transform c in cavalryListContainer) Destroy(c.gameObject);
        if (distanceListContainer != null)
            foreach (Transform c in distanceListContainer) Destroy(c.gameObject);

        // Cargar la base de datos de escuadrones
        var squadDatabase = Resources.Load<SquadDatabase>("Data/Squads/SquadDatabase");
        if (squadDatabase == null)
        {
            Debug.LogWarning("[BarracksMenuUIController] No se pudo cargar SquadDatabase");
            return;
        }

        // Mostrar los escuadrones del héroe en cada lista según unitType
        foreach (var squadInstance in heroData.squadProgress)
        {
            var squadData = squadDatabase.allSquads.Find(sq => sq != null && sq.id == squadInstance.baseSquadID);
            if (squadData == null) continue;
            Transform targetList = null;
            switch (squadData.unitType)
            {
                case UnitType.Infantry: targetList = infantryListContainer; break;
                case UnitType.Cavalry: targetList = cavalryListContainer; break;
                case UnitType.Distance: targetList = distanceListContainer; break;
            }
            if (targetList != null && squadListItemPrefab != null)
            {
                var itemGO = Instantiate(squadListItemPrefab, targetList);
                var optionUI = itemGO.GetComponent<SquadOptionUI>();
                optionUI?.SetSquadData(squadData);
                optionUI.setProgress(squadInstance.level.ToString());
            }
        }

        // Actualizar textos de experiencia y espacios
        if (expText != null && heroData != null)
        {
            // expText.text = $"EXP Unidad: {heroData.sharedUnitExp}/{heroData.maxSharedUnitExp}";
        }
        if (barracksSlotsText != null && heroData != null)
        {
            barracksSlotsText.text = $"Espacios: {heroData.squadProgress.Count}/10";
        }

        Debug.Log($"[BarracksMenuUIController] Abriendo menú de barracas para: {heroData?.heroName ?? "(null)"}");
    }

    public void Close()
    {
        if (mainPanel != null)
            mainPanel.SetActive(false);
            DialogueUIState.IsDialogueOpen = false;
            if (HeroCameraController.Instance != null)
                HeroCameraController.Instance.SetCameraFollowEnabled(true);
            
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
    }

    // Lógica para abrir el panel de selección de escuadrón
    public void OnAddUnitClicked(UnitType unitType)
    {
        if (squadSelectionPanel != null && _currentHeroData != null)
        {
            Debug.Log($"[BarracksMenuUIController] Abriendo panel de selección de escuadrón para héroe: {_currentHeroData.heroName}, tipo: {unitType}");
            squadSelectionPanel.OnSquadSelected = OnSquadSelectedFromPanel;
            squadSelectionPanel.Open(_currentHeroData, unitType);
        }
        else
        {
            Debug.LogWarning("[BarracksMenuUIController] No hay SquadSelectionPanel o HeroData");
        }
    }

    // Callback cuando se selecciona un escuadrón en el panel
    private void OnSquadSelectedFromPanel(SquadData squadData)
    {
        Debug.Log($"[BarracksMenuUIController] Agregar escuadrón '{squadData.squadName}' al héroe: {_currentHeroData?.heroName ?? "(null)"}");
        if (_currentHeroData == null || squadData == null)
        {
            Debug.LogWarning("[BarracksMenuUIController] HeroData o SquadData nulo al intentar enlistar unidad");
            return;
        }

        // Crear nueva instancia de escuadrón
        var newSquad = new SquadInstanceData
        {
            id = System.Guid.NewGuid().ToString(), // ID único para la instancia
            baseSquadID = squadData.id,
            level = 1,
            experience = 0,
            unlockedAbilities = new System.Collections.Generic.List<string>(),
            unlockedFormationsIndices = new System.Collections.Generic.List<int>(),
            selectedFormationIndex = 0,
            customName = squadData.squadName
        };

        _currentHeroData.squadProgress.Add(newSquad);
        Debug.Log($"[BarracksMenuUIController] Escuadrón '{squadData.squadName}' enlistado correctamente. Total ahora: {_currentHeroData.squadProgress.Count}");

        // Actualizar la sesión (SelectedHero) y guardar el PlayerData completo
        if (PlayerSessionService.CurrentPlayer != null)
        {
            // Buscar el HeroData en la lista de héroes y actualizar referencia
            var heroList = PlayerSessionService.CurrentPlayer.heroes;
            for (int i = 0; i < heroList.Count; i++)
            {
                if (heroList[i] == _currentHeroData || heroList[i].heroName == _currentHeroData.heroName)
                {
                    heroList[i] = _currentHeroData;
                    break;
                }
            }
            PlayerSessionService.SetSelectedHero(_currentHeroData);
            SaveSystem.SavePlayer(PlayerSessionService.CurrentPlayer);
        }
        else
        {
            Debug.LogWarning("[BarracksMenuUIController] No hay CurrentPlayer en sesión para guardar los datos");
        }

        // Refrescar la UI de listas para mostrar la nueva unidad
        Open(_currentHeroData);
    }
}
