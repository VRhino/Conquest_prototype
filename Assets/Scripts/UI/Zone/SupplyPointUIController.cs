using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Controls the contextual UI displayed when the player is inside a supply point.
/// Flow: hero enters allied zone → "Press F" prompt → F opens squad panel →
/// select squad → Accept → channeling via HUD bar → swap executed.
/// </summary>
public class SupplyPointUIController : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] CanvasGroup _panelGroup;
    [SerializeField] TroopsViewerController _troopsViewer;
    [SerializeField] CanvasGroup _unavailableGroup;
    [SerializeField] GameObject _healingIndicator;

    [Header("Interaction Prompt")]
    [SerializeField] GameObject _interactPrompt;
    [SerializeField] float _interactionPromptRadius = 4f;

    [Header("Accept")]
    [SerializeField] Button _acceptButton;

    [Header("Cooldown")]
    [SerializeField] GameObject _cooldownIndicator;
    [SerializeField] TMPro.TextMeshProUGUI _cooldownText;

    EntityManager _em;
    int _currentZoneId = -1;
    Team _playerTeam = Team.None;
    List<string> _currentSquadIds = new();
    List<SquadInstanceData> _squadInstances;
    int _lastActiveSquadId = -1;

    bool _isPanelOpen = false;
    int _selectedSquadId = -1;
    bool _wasChanneling = false;
    HUDController _hud;

    void Start()
    {
        _em = World.DefaultGameObjectInjectionWorld.EntityManager;
        CacheSquadInstances();
        GetPlayerTeam();
        InitializeViewer();
        _hud = FindAnyObjectByType<HUDController>();

        if (_acceptButton != null)
        {
            _acceptButton.onClick.AddListener(OnAcceptClicked);
            _acceptButton.interactable = false;
        }
    }

    /// <summary>
    /// Caches the local hero's squad instances from BattleSceneController or PlayerSession.
    /// </summary>
    void CacheSquadInstances()
    {
        // Try to get battle-specific squad instances from BattleSceneController
        var battleController = FindAnyObjectByType<BattleSceneController>();
        if (battleController != null && battleController.CurrentBattleData != null)
        {
            var heroName = PlayerSessionService.SelectedHero?.heroName;
            if (!string.IsNullOrEmpty(heroName))
            {
                var battleHero = battleController.CurrentBattleData.findHeroDataByName(heroName);
                if (battleHero != null)
                {
                    _squadInstances = battleHero.squadInstances;
                    return;
                }
            }
        }

        // Fallback: use hero's squad progress directly
        var hero = PlayerSessionService.SelectedHero;
        if (hero != null)
            _squadInstances = hero.squadProgress;
    }

    void GetPlayerTeam()
    {
        var q = _em.CreateEntityQuery(ComponentType.ReadOnly<DataContainerComponent>());
        if (!q.IsEmptyIgnoreFilter)
        {
            var data = q.GetSingleton<DataContainerComponent>();
            _playerTeam = (Team)data.teamID;
        }
    }

    void InitializeViewer()
    {
        if (_troopsViewer == null) return;

        _troopsViewer.ConnectExternalEvents(
            onItemClicked: OnSquadClicked,
            onItemsRequested: OnCreateSquadOption,
            onPlaceholderClicked: null
        );
        _troopsViewer.initialize(4);
    }

    /// <summary>
    /// Rebuilds the list of available squad IDs (excluding active and eliminated).
    /// </summary>
    void RefreshSquadList()
    {
        if (_troopsViewer == null) return;

        var q = _em.CreateEntityQuery(ComponentType.ReadOnly<DataContainerComponent>());
        if (q.IsEmptyIgnoreFilter) return;

        var data = q.GetSingleton<DataContainerComponent>();

        // Get the active squad ID
        int activeSquadId = -1;
        var heroQuery = _em.CreateEntityQuery(
            ComponentType.ReadOnly<IsLocalPlayer>(),
            ComponentType.ReadOnly<HeroSquadSelectionComponent>());
        if (!heroQuery.IsEmptyIgnoreFilter)
        {
            var selection = heroQuery.GetSingleton<HeroSquadSelectionComponent>();
            activeSquadId = selection.instanceId;
        }

        // Get eliminated squad IDs
        HashSet<int> eliminatedIds = new();
        Entity heroEntity = Entity.Null;
        var heroLifeQuery = _em.CreateEntityQuery(
            ComponentType.ReadOnly<IsLocalPlayer>(),
            ComponentType.ReadOnly<HeroLifeComponent>());
        if (!heroLifeQuery.IsEmptyIgnoreFilter)
        {
            heroEntity = heroLifeQuery.GetSingletonEntity();
            if (_em.HasBuffer<InactiveSquadElement>(heroEntity))
            {
                var inactiveBuffer = _em.GetBuffer<InactiveSquadElement>(heroEntity);
                for (int i = 0; i < inactiveBuffer.Length; i++)
                {
                    if (inactiveBuffer[i].isEliminated)
                        eliminatedIds.Add(inactiveBuffer[i].squadId);
                }
            }
        }

        // Build available squad list (as string IDs for TroopsViewerController)
        _currentSquadIds.Clear();
        foreach (int squadId in data.selectedSquads)
        {
            if (squadId == activeSquadId) continue;
            if (eliminatedIds.Contains(squadId)) continue;

            _currentSquadIds.Add(squadId.ToString());
        }

        _troopsViewer.SetItems(new List<string>(_currentSquadIds), "SupplyPointUI");
    }

    /// <summary>
    /// Callback from TroopsViewerController to create a SquadOptionUI for a given squad.
    /// </summary>
    GameObject OnCreateSquadOption(string squadIdStr, Transform container, GameObject prefab)
    {
        if (prefab == null || container == null) return null;
        if (!int.TryParse(squadIdStr, out int squadId)) return null;

        // Look up the baseSquadID from the SquadIdMapElement buffer
        string baseSquadID = GetBaseSquadID(squadId);
        if (string.IsNullOrEmpty(baseSquadID)) return null;

        SquadData squadData = SquadDataService.GetSquadById(baseSquadID);
        if (squadData == null) return null;

        GameObject optionGO = Instantiate(prefab, container);
        optionGO.name = squadIdStr;
        SquadOptionUI optionUI = optionGO.GetComponent<SquadOptionUI>();
        if (optionUI != null)
        {
            optionUI.SetSquadData(squadData);
            optionUI.ToggleBattlePreMode();

            // Find matching squad instance for level and alive count
            SquadInstanceData instance = FindSquadInstance(baseSquadID);
            if (instance != null)
                optionUI.SetInstanceData(instance.level.ToString(), instance.unitsAlive);

            optionUI.SetSelected(false);

            // Wire click to trigger TroopsViewerController's item click
            optionUI.onClick = () => _troopsViewer.TriggerItemClick(squadIdStr);
        }

        return optionGO;
    }

    /// <summary>
    /// Callback when a squad option is clicked — selects it instead of immediate swap.
    /// </summary>
    void OnSquadClicked(string squadIdStr)
    {
        if (!int.TryParse(squadIdStr, out int squadId)) return;

        _selectedSquadId = squadId;

        // Highlight the selected option
        var options = _troopsViewer.GetComponentsInChildren<SquadOptionUI>();
        foreach (var opt in options)
            opt.SetSelected(opt.gameObject.name == squadIdStr);

        if (_acceptButton != null)
            _acceptButton.interactable = true;
    }

    /// <summary>
    /// Resolves an int squad ID to its base string ID using the SquadIdMapElement buffer.
    /// </summary>
    string GetBaseSquadID(int squadId)
    {
        var q = _em.CreateEntityQuery(ComponentType.ReadOnly<DataContainerComponent>());
        if (q.IsEmptyIgnoreFilter) return null;

        Entity dcEntity = q.GetSingletonEntity();
        if (!_em.HasBuffer<SquadIdMapElement>(dcEntity)) return null;

        var mapBuffer = _em.GetBuffer<SquadIdMapElement>(dcEntity);
        for (int i = 0; i < mapBuffer.Length; i++)
        {
            if (mapBuffer[i].squadId == squadId)
                return mapBuffer[i].baseSquadID.ToString();
        }
        return null;
    }

    /// <summary>
    /// Finds the SquadInstanceData matching a base squad ID from the cached instances.
    /// </summary>
    SquadInstanceData FindSquadInstance(string baseSquadID)
    {
        if (_squadInstances == null) return null;
        foreach (var inst in _squadInstances)
        {
            if (inst.baseSquadID == baseSquadID)
                return inst;
        }
        return null;
    }

    void Update()
    {
        // Re-fetch team if not yet resolved (DataContainer may not be ready at Start)
        if (_playerTeam == Team.None)
            GetPlayerTeam();

        // 1. Find local hero entity and position
        var heroQuery = _em.CreateEntityQuery(
            ComponentType.ReadOnly<IsLocalPlayer>(),
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.ReadOnly<HeroLifeComponent>());
        if (heroQuery.IsEmptyIgnoreFilter)
        {
            if (_interactPrompt != null) _interactPrompt.SetActive(false);
            ClosePanel();
            HidePanel();
            return;
        }

        Entity heroEnt = heroQuery.GetSingletonEntity();
        float3 heroPos = _em.GetComponentData<LocalTransform>(heroEnt).Position;

        // 2. Find which supply zone (if any) contains the hero
        Entity zoneEntity = Entity.Null;
        int foundZoneId = -1;
        var zoneQuery = _em.CreateEntityQuery(
            ComponentType.ReadOnly<ZoneTriggerComponent>(),
            ComponentType.ReadOnly<SupplyPointComponent>(),
            ComponentType.ReadOnly<LocalTransform>());
        using (var zones = zoneQuery.ToEntityArray(Allocator.Temp))
        {
            foreach (var z in zones)
            {
                var zt = _em.GetComponentData<ZoneTriggerComponent>(z);
                var lt = _em.GetComponentData<LocalTransform>(z);
                float distSq = math.distancesq(heroPos, lt.Position);
                if (distSq <= _interactionPromptRadius * _interactionPromptRadius)
                {
                    zoneEntity = z;
                    foundZoneId = zt.zoneId;
                    break;
                }
            }
        }

        if (zoneEntity == Entity.Null)
        {
            if (_interactPrompt != null) _interactPrompt.SetActive(false);
            ClosePanel();
            HidePanel();
            return;
        }

        _currentZoneId = foundZoneId;

        // 3. Read zone state
        var zone = _em.GetComponentData<ZoneTriggerComponent>(zoneEntity);
        var supply = _em.GetComponentData<SupplyPointComponent>(zoneEntity);

        bool available = !_unavailableGroup || (!supply.isContested && zone.teamOwner == (int)_playerTeam);

        Debug.Log($"[SupplyUI] zone={foundZoneId} owner={zone.teamOwner} playerTeam={(int)_playerTeam} " +
                  $"contested={supply.isContested} available={available} " +
                  $"promptRef={_interactPrompt != null} panelOpen={_isPanelOpen}");

        if (_unavailableGroup != null)
            _unavailableGroup.alpha = available ? 0f : 1f;
        if (_healingIndicator != null)
            _healingIndicator.SetActive(available);

        bool isChanneling = _em.HasComponent<SquadSwapChannelingComponent>(heroEnt);
        bool onCooldown = _em.HasComponent<SquadSwapCooldownComponent>(heroEnt);

        // 4. Show "Press F" prompt when available and panel not open
        if (_interactPrompt != null)
            _interactPrompt.SetActive(available && !_isPanelOpen && !onCooldown && !isChanneling);

        // 5. Detect F press to open panel
        if (available && !_isPanelOpen && !onCooldown && !isChanneling)
        {
            if (_em.HasComponent<PlayerInteractionComponent>(heroEnt))
            {
                var interaction = _em.GetComponentData<PlayerInteractionComponent>(heroEnt);
                if (interaction.interactPressed)
                    ShowPanel();
            }
        }

        // 6. Detect Escape to close panel
        if (_isPanelOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            ClosePanel();

        // 7. Auto-close panel if conditions no longer met
        if (_isPanelOpen && !available)
            ClosePanel();

        // 8. Channeling — use HUD progress bar
        if (isChanneling)
        {
            var channeling = _em.GetComponentData<SquadSwapChannelingComponent>(heroEnt);
            float fill = channeling.duration > 0f ? channeling.timer / channeling.duration : 0f;
            if (_hud != null)
                _hud.ShowProgressBar("Channeling...", fill);
            _wasChanneling = true;
        }
        else if (_wasChanneling)
        {
            if (_hud != null)
                _hud.HideProgressBar();
            _wasChanneling = false;
        }

        // 9. Cooldown indicator
        if (_cooldownIndicator != null)
        {
            if (onCooldown)
            {
                var cooldown = _em.GetComponentData<SquadSwapCooldownComponent>(heroEnt);
                _cooldownIndicator.SetActive(true);
                if (_cooldownText != null)
                    _cooldownText.text = Mathf.CeilToInt(cooldown.remainingTime).ToString();
            }
            else
            {
                _cooldownIndicator.SetActive(false);
            }
        }

        // 10. Refresh squad list when active squad changes (after a swap)
        var selQuery = _em.CreateEntityQuery(
            ComponentType.ReadOnly<IsLocalPlayer>(),
            ComponentType.ReadOnly<HeroSquadSelectionComponent>());
        if (!selQuery.IsEmptyIgnoreFilter)
        {
            int currentActive = selQuery.GetSingleton<HeroSquadSelectionComponent>().instanceId;
            if (currentActive != _lastActiveSquadId)
            {
                _lastActiveSquadId = currentActive;
                if (_isPanelOpen)
                    RefreshSquadList();
            }
        }

        // Refresh squad list when inactive buffer changes (eliminated squads)
        if (_isPanelOpen && _em.HasBuffer<InactiveSquadElement>(heroEnt))
        {
            var inactiveBuffer = _em.GetBuffer<InactiveSquadElement>(heroEnt);
            int eliminatedCount = 0;
            for (int i = 0; i < inactiveBuffer.Length; i++)
            {
                if (inactiveBuffer[i].isEliminated)
                    eliminatedCount++;
            }

            int expectedAvailable = CountAvailableSquads(eliminatedCount);
            if (expectedAvailable != _currentSquadIds.Count)
                RefreshSquadList();
        }

        // Keep panel hidden if not open (don't auto-show by proximity)
        if (!_isPanelOpen)
            HidePanel();
    }

    void ShowPanel()
    {
        _isPanelOpen = true;
        _selectedSquadId = -1;
        if (_acceptButton != null) _acceptButton.interactable = false;
        if (_panelGroup != null)
        {
            _panelGroup.alpha = 1f;
            _panelGroup.interactable = true;
            _panelGroup.blocksRaycasts = true;
        }
        if (_interactPrompt != null) _interactPrompt.SetActive(false);
        RefreshSquadList();
        DialogueUIState.IsDialogueOpen = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (HeroCameraController.Instance != null)
            HeroCameraController.Instance.SetCameraFollowEnabled(false);
    }

    void ClosePanel()
    {
        if (!_isPanelOpen) return;
        _isPanelOpen = false;
        _selectedSquadId = -1;
        if (_acceptButton != null) _acceptButton.interactable = false;
        DialogueUIState.IsDialogueOpen = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (HeroCameraController.Instance != null)
            HeroCameraController.Instance.SetCameraFollowEnabled(true);
        HidePanel();
    }

    void OnDestroy()
    {
        if (_isPanelOpen)
        {
            DialogueUIState.IsDialogueOpen = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (HeroCameraController.Instance != null)
                HeroCameraController.Instance.SetCameraFollowEnabled(true);
        }
    }

    void OnAcceptClicked()
    {
        if (_selectedSquadId < 0) return;
        RequestSwap(_selectedSquadId);
        ClosePanel();
    }

    void HidePanel()
    {
        _currentZoneId = -1;
        if (_panelGroup != null)
        {
            _panelGroup.alpha = 0f;
            _panelGroup.interactable = false;
            _panelGroup.blocksRaycasts = false;
        }
    }

    /// <summary>
    /// Counts how many squads should be available given the eliminated count.
    /// </summary>
    int CountAvailableSquads(int eliminatedCount)
    {
        var q = _em.CreateEntityQuery(ComponentType.ReadOnly<DataContainerComponent>());
        if (q.IsEmptyIgnoreFilter) return 0;

        var data = q.GetSingleton<DataContainerComponent>();

        int activeSquadId = -1;
        var heroQuery = _em.CreateEntityQuery(
            ComponentType.ReadOnly<IsLocalPlayer>(),
            ComponentType.ReadOnly<HeroSquadSelectionComponent>());
        if (!heroQuery.IsEmptyIgnoreFilter)
        {
            var selection = heroQuery.GetSingleton<HeroSquadSelectionComponent>();
            activeSquadId = selection.instanceId;
        }

        int total = 0;
        foreach (int squadId in data.selectedSquads)
        {
            if (squadId != activeSquadId)
                total++;
        }
        return total - eliminatedCount;
    }

    /// <summary>Requests a squad swap to the specified squad.</summary>
    private void RequestSwap(int squadId)
    {
        var heroQuery = _em.CreateEntityQuery(
            ComponentType.ReadOnly<IsLocalPlayer>(),
            ComponentType.ReadOnly<HeroLifeComponent>());
        if (heroQuery.IsEmptyIgnoreFilter)
            return;
        Entity hero = heroQuery.GetSingletonEntity();

        // Block if on cooldown or already channeling
        if (_em.HasComponent<SquadSwapCooldownComponent>(hero) ||
            _em.HasComponent<SquadSwapChannelingComponent>(hero))
            return;

        var req = new SquadSwapRequest { newSquadId = squadId, zoneId = _currentZoneId };
        if (_em.HasComponent<SquadSwapRequest>(hero))
            _em.SetComponentData(hero, req);
        else
            _em.AddComponentData(hero, req);
    }
}
