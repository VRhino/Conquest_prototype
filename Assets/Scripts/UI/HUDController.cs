using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Handles real time updates for the in-game HUD during combat.
/// Reads UIHeroBattleDataComponent (written by UIBattleDataSystem) instead of
/// querying gameplay ECS components directly.
/// </summary>
public class HUDController : MonoBehaviour
{
    [System.Serializable]
    public class AbilitySlot
    {
        public Image icon;
        public Image cooldownFill;
        [Tooltip("Entity that holds a CooldownComponent")] public Entity abilityEntity = Entity.Null;

        /// <summary>Updates the cooldown display.</summary>
        public void UpdateSlot(EntityManager em)
        {
            if (abilityEntity == Entity.Null || cooldownFill == null)
                return;

            if (em.HasComponent<CooldownComponent>(abilityEntity))
            {
                var cd = em.GetComponentData<CooldownComponent>(abilityEntity);
                if (cd.cooldownDuration > 0f)
                    cooldownFill.fillAmount = cd.currentCooldown / cd.cooldownDuration;
                else
                    cooldownFill.fillAmount = 0f;

                // Flash or hide fill when ready
                cooldownFill.enabled = !cd.isReady;
            }
        }
    }

    [Header("Section Toggles")]
    [SerializeField] GameObject _heroSection;
    [SerializeField] GameObject _abilitiesSection;
    [SerializeField] GameObject _squadSection;

    [Header("Hero Elements")]
    [SerializeField] Image _healthFill;
    [SerializeField] Image _staminaFill;
    [SerializeField] TMP_Text _healthText;
    [SerializeField] TMP_Text _staminaText;

    [Header("Ability Slots")]
    [SerializeField] AbilitySlot[] _abilitySlots = new AbilitySlot[4];

    [Header("Squad Section")]
    [SerializeField] SquadSectionController _squadSectionController;

    [Header("Battle Timer")]
    [SerializeField] TMP_Text _battleTimerText;

    [Header("Capture Bar")]
    [SerializeField] GameObject _captureBarSection;
    [SerializeField] Image _captureProgressFill;
    [SerializeField] TMP_Text _captureProgressText;

    [Header("Capture Point Icons")]
    [SerializeField] Transform _capturePointIconsContainer;
    [SerializeField] GameObject _capturePointIconPrefab;

    private Dictionary<int, GameObject> _capturePointIcons = new();
    private bool _squadInitialized;
    private bool _externalOverride;

    // ── Cached ECS references ─────────────────────────────────────────────────
    private World        _world;
    private EntityManager _em;
    private EntityQuery  _uiDataQuery;
    private EntityQuery  _captureIconQuery;
    private EntityQuery  _dataContainerQuery;

    void Awake()
    {
        _world = World.DefaultGameObjectInjectionWorld;
        _em    = _world.EntityManager;

        _uiDataQuery       = _em.CreateEntityQuery(ComponentType.ReadOnly<UIHeroBattleDataComponent>());
        _captureIconQuery  = _em.CreateEntityQuery(
            ComponentType.ReadOnly<CapturePointTag>(),
            ComponentType.ReadOnly<ZoneTriggerComponent>());
        _dataContainerQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<DataContainerComponent>());
    }

    void OnDestroy()
    {
        _uiDataQuery.Dispose();
        _captureIconQuery.Dispose();
        _dataContainerQuery.Dispose();
    }

    void Update()
    {
        if (_uiDataQuery.IsEmptyIgnoreFilter) return;
        var data = _em.GetComponentData<UIHeroBattleDataComponent>(_uiDataQuery.GetSingletonEntity());

        UpdateHeroSection(data);
        CheckSquadChangeEvent(data);
        InitializeSquadIfNeeded();
        _squadSectionController?.UpdateFromECS(_em);
        UpdateCaptureBar(data);
        UpdateCapturePointIcons();
    }

    void UpdateHeroSection(UIHeroBattleDataComponent data)
    {
        if (_heroSection != null && !_heroSection.activeSelf)
            return;

        if (_healthFill != null)
            _healthFill.fillAmount = data.maxHealth > 0f ? data.currentHealth / data.maxHealth : 0f;
        if (_staminaFill != null)
            _staminaFill.fillAmount = data.maxStamina > 0f ? data.currentStamina / data.maxStamina : 0f;
        if (_healthText != null)
            _healthText.text = $"{Mathf.CeilToInt(data.currentHealth)} / {Mathf.CeilToInt(data.maxHealth)}";
        if (_staminaText != null)
            _staminaText.text = $"{Mathf.CeilToInt(data.currentStamina)} / {Mathf.CeilToInt(data.maxStamina)}";

        foreach (var slot in _abilitySlots)
            slot?.UpdateSlot(_em);
    }

    void CheckSquadChangeEvent(UIHeroBattleDataComponent data)
    {
        // UIBattleDataSystem consumed the SquadChangeEvent entity — we just read the result.
        if (!data.squadChangedThisFrame) return;

        if (_dataContainerQuery.IsEmptyIgnoreFilter) return;
        var dcEntity = _dataContainerQuery.GetSingletonEntity();
        if (_em.HasBuffer<SquadIdMapElement>(dcEntity))
        {
            var mapBuffer = _em.GetBuffer<SquadIdMapElement>(dcEntity);
            for (int i = 0; i < mapBuffer.Length; i++)
            {
                if (mapBuffer[i].squadId == data.newSquadId)
                {
                    string baseId   = mapBuffer[i].baseSquadID.ToString();
                    var    squadData = SquadDataService.GetSquadById(baseId);
                    if (squadData != null && _squadSectionController != null)
                        _squadSectionController.Initialize(squadData);
                    break;
                }
            }
        }
    }

    void InitializeSquadIfNeeded()
    {
        if (_squadInitialized || _squadSectionController == null) return;
        if (_dataContainerQuery.IsEmptyIgnoreFilter) return;

        var dc      = _em.GetComponentData<DataContainerComponent>(_dataContainerQuery.GetSingletonEntity());
        string squadId = dc.selectedSquadBaseID.ToString();
        if (string.IsNullOrEmpty(squadId)) return;

        var squadData = SquadDataService.GetSquadById(squadId);
        if (squadData == null) return;

        _squadSectionController.Initialize(squadData);
        _squadInitialized = true;
    }

    /// <summary>Shows the capture bar with custom text and fill (for channeling).</summary>
    public void ShowProgressBar(string text, float fillAmount)
    {
        _externalOverride = true;
        if (_captureBarSection != null) _captureBarSection.SetActive(true);
        if (_captureProgressFill != null) _captureProgressFill.fillAmount = fillAmount;
        if (_captureProgressText != null) _captureProgressText.text = text;
    }

    /// <summary>Hides the bar and restores normal capture behavior.</summary>
    public void HideProgressBar()
    {
        _externalOverride = false;
        if (_captureBarSection != null) _captureBarSection.SetActive(false);
    }

    void UpdateCaptureBar(UIHeroBattleDataComponent data)
    {
        if (_externalOverride) return;

        if (_captureBarSection != null)
            _captureBarSection.SetActive(data.isInCaptureZone);

        if (!data.isInCaptureZone) return;

        if (_captureProgressFill != null)
            _captureProgressFill.fillAmount = data.captureProgress / 100f;

        if (_captureProgressText != null)
            _captureProgressText.text = $"{Mathf.RoundToInt(data.captureProgress)}%";
    }

    void UpdateCapturePointIcons()
    {
        if (_capturePointIconsContainer == null || _capturePointIconPrefab == null)
            return;

        // Build set of zoneIds that should be visible along with label data
        var visibleZones = new Dictionary<int, (byte label, bool isFinal)>();
        var entities = _captureIconQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
        for (int i = 0; i < entities.Length; i++)
        {
            var zone = _em.GetComponentData<ZoneTriggerComponent>(entities[i]);
            if (zone.isActive && !zone.isLocked)
                visibleZones[zone.zoneId] = (zone.pointLabel, zone.isFinal);
        }
        entities.Dispose();

        // Remove icons that should no longer be visible
        var toRemove = new List<int>();
        foreach (var kvp in _capturePointIcons)
        {
            if (!visibleZones.ContainsKey(kvp.Key))
            {
                Destroy(kvp.Value);
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var id in toRemove)
            _capturePointIcons.Remove(id);

        // Add icons for newly visible zones
        foreach (var kvp in visibleZones)
        {
            if (!_capturePointIcons.ContainsKey(kvp.Key))
            {
                var icon = Instantiate(_capturePointIconPrefab, _capturePointIconsContainer);
                var controller = icon.GetComponent<CapturePointIconControllerUI>();
                if (controller != null)
                {
                    controller.SetColors(false); // enemy-owned by default
                    var (label, isFinal) = kvp.Value;
                    string labelStr = label > 0 ? ((char)label).ToString() : "";
                    controller.SetLabel(labelStr, isFinal);
                }
                _capturePointIcons[kvp.Key] = icon;
            }
        }
    }
}
