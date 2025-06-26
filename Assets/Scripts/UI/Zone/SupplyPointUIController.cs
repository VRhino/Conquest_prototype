using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the contextual UI displayed when the player is inside a supply point.
/// Provides squad swap buttons and healing indicators.
/// </summary>
public class SupplyPointUIController : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] CanvasGroup _panelGroup;
    [SerializeField] RectTransform _buttonContainer;
    [SerializeField] SquadButtonUI _buttonPrefab;
    [SerializeField] CanvasGroup _unavailableGroup;
    [SerializeField] GameObject _healingIndicator;

    [Header("World Tracking")]
    [SerializeField] Transform _worldTarget;
    [SerializeField] float _offsetY = 2f;

    /// <summary>Identifier of the zone this UI represents.</summary>
    public int zoneId;

    readonly List<SquadButtonUI> _buttons = new();
    EntityManager _em;
    Entity _zoneEntity;
    Team _playerTeam = Team.None;

    void Start()
    {
        _em = World.DefaultGameObjectInjectionWorld.EntityManager;
        FindZoneEntity();
        InitializeButtons();
        GetPlayerTeam();
    }

    void FindZoneEntity()
    {
        var query = _em.CreateEntityQuery(ComponentType.ReadOnly<ZoneTriggerComponent>());
        using NativeArray<Entity> ents = query.ToEntityArray(Allocator.Temp);
        foreach (var ent in ents)
        {
            var zone = _em.GetComponentData<ZoneTriggerComponent>(ent);
            if (zone.zoneId == zoneId && zone.zoneType == ZoneType.Supply)
            {
                _zoneEntity = ent;
                break;
            }
        }
    }

    void InitializeButtons()
    {
        if (_buttonPrefab == null || _buttonContainer == null)
            return;

        var q = _em.CreateEntityQuery(ComponentType.ReadOnly<DataContainerComponent>());
        if (q.IsEmptyIgnoreFilter)
            return;

        var data = q.GetSingleton<DataContainerComponent>();
        foreach (int squadId in data.selectedSquads)
        {
            var btn = Instantiate(_buttonPrefab, _buttonContainer);
            btn.Setup(squadId, this);
            _buttons.Add(btn);
        }
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

    void Update()
    {
        if (_zoneEntity == Entity.Null || !_em.Exists(_zoneEntity))
            return;

        if (_worldTarget != null)
        {
            Vector3 pos = _worldTarget.position + Vector3.up * _offsetY;
            transform.position = Camera.main.WorldToScreenPoint(pos);
        }

        var zone = _em.GetComponentData<ZoneTriggerComponent>(_zoneEntity);
        if (!_em.HasComponent<SupplyPointComponent>(_zoneEntity))
            return;
        var supply = _em.GetComponentData<SupplyPointComponent>(_zoneEntity);

        bool available = !_unavailableGroup || (!supply.isContested && zone.teamOwner == (int)_playerTeam);
        if (_panelGroup != null)
            _panelGroup.alpha = available ? 1f : 0f;
        if (_unavailableGroup != null)
            _unavailableGroup.alpha = available ? 0f : 1f;
        if (_healingIndicator != null)
            _healingIndicator.SetActive(available);
    }

    /// <summary>Requests a squad swap to the specified squad.</summary>
    public void RequestSwap(int squadId)
    {
        var heroQuery = _em.CreateEntityQuery(
            ComponentType.ReadOnly<IsLocalPlayer>(),
            ComponentType.ReadOnly<HeroLifeComponent>());
        if (heroQuery.IsEmptyIgnoreFilter)
            return;
        Entity hero = heroQuery.GetSingletonEntity();
        var req = new SquadSwapRequest { newSquadId = squadId, zoneId = zoneId };
        if (_em.HasComponent<SquadSwapRequest>(hero))
            _em.SetComponentData(hero, req);
        else
            _em.AddComponentData(hero, req);
    }
}
