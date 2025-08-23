using UnityEngine;

using UnityEngine.UI;

using System;
using System.Linq;
public class BarracksMenuUIController : MonoBehaviour, IFullscreenPanel
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

    [Header("Panel de detalle de escuadrón")]
    public SquadDetailPanel squadDetailPanel;

    [Header("Botones de acción")]
    public Button exitButton;
    public Button addInfantryButton;
    public Button addCavalryButton;
    public Button addDistanceButton;

    [Header("Textos de información")]
    public TMPro.TextMeshProUGUI expText;
    public TMPro.TextMeshProUGUI barracksSlotsText;


    private HeroData _currentHeroData;

    // IFullscreenPanel interface implementation
    public bool IsPanelOpen => mainPanel != null && mainPanel.activeSelf;

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
            exitButton.onClick.AddListener(() => FullscreenPanelManager.Instance.ClosePanel<BarracksMenuUIController>());
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
                optionUI.onClick = () => showSquadDetails(squadInstance);
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
    public void showSquadDetails(SquadInstanceData squadDataAndProgress)
    {
        if (squadDataAndProgress == null)
        {
            Debug.LogWarning("[BarracksMenuUIController] SquadData nulo al intentar mostrar detalles");
            return;
        }
        // Buscar el SquadData correspondiente
        var squadDatabase = Resources.Load<SquadDatabase>("Data/Squads/SquadDatabase");
        if (squadDatabase == null)
        {
            Debug.LogWarning("[BarracksMenuUIController] No se pudo cargar SquadDatabase para detalles");
            return;
        }
        var squadData = squadDatabase.allSquads.Find(sq => sq != null && sq.id == squadDataAndProgress.baseSquadID);
        if (squadData == null)
        {
            Debug.LogWarning($"[BarracksMenuUIController] No se encontró SquadData para baseSquadID: {squadDataAndProgress.baseSquadID}");
            return;
        }
        if (squadDetailPanel != null)
        {
            squadDetailPanel.Show(squadDataAndProgress, squadData);
        }
        else
        {
            Debug.LogWarning("[BarracksMenuUIController] No se asignó SquadDetailPanel en el inspector");
        }
    }
    public void Close()
    {
        if (mainPanel != null)
            mainPanel.SetActive(false);
    }

    #region IFullscreenPanel Implementation

    /// <summary>
    /// Abre el panel sin pasar datos específicos. Usa el héroe actual del PlayerSessionService.
    /// </summary>
    public void OpenPanel()
    {
        var heroData = PlayerSessionService.SelectedHero;
        if (heroData != null)
        {
            Open(heroData);
        }
        else
        {
            Debug.LogError("[BarracksMenuUIController] No hay héroe activo en PlayerSessionService para abrir las barracas");
        }
    }

    /// <summary>
    /// Cierra el panel.
    /// </summary>
    public void ClosePanel()
    {
        Close();
    }

    /// <summary>
    /// Alterna el estado del panel (abierto/cerrado).
    /// </summary>
    public void TogglePanel()
    {
        if (IsPanelOpen)
        {
            ClosePanel();
        }
        else
        {
            OpenPanel();
        }
    }

    #endregion

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
            //add the index of all grid formations on squadData
            permittedFormationIndexes = squadData.gridFormations.Select((f, i) => i).ToList(),
            selectedFormationIndex = 0,
            customName = squadData.squadName,
            unitsInSquad = squadData.unitCount,
        };

        _currentHeroData.squadProgress.Add(newSquad);

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
