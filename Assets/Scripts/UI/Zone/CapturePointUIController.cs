using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls the UI feedback for a capture point when the player is inside its radius.
/// Reads ECS components to display capture progress, contest state and ownership.
/// </summary>
public class CapturePointUIController : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] Image _progressFill;
    [SerializeField] CanvasGroup _contestedGroup;
    [SerializeField] Image _lockIcon;
    [SerializeField] TMP_Text _label;

    [Header("Colors")]
    [SerializeField] Color _neutralColor = Color.gray;
    [SerializeField] Color _playerColor = Color.blue;
    [SerializeField] Color _enemyColor = Color.red;

    [Header("World Tracking")]
    [SerializeField] Transform _worldTarget;
    [SerializeField] float _offsetY = 2f;

    /// <summary>Identifier of the zone this UI represents.</summary>
    public int zoneId;

    EntityManager _em;
    Entity _zoneEntity;

    void Start()
    {
        _em = World.DefaultGameObjectInjectionWorld.EntityManager;
        FindZoneEntity();
    }

    void FindZoneEntity()
    {
        if (_em == null)
            return;

        var query = _em.CreateEntityQuery(ComponentType.ReadOnly<ZoneTriggerComponent>());
        using NativeArray<Entity> ents = query.ToEntityArray(Allocator.Temp);
        foreach (var ent in ents)
        {
            var zone = _em.GetComponentData<ZoneTriggerComponent>(ent);
            if (zone.zoneId == zoneId && zone.zoneType == ZoneType.Capture)
            {
                _zoneEntity = ent;
                break;
            }
        }
    }

    void Update()
    {
        if (_zoneEntity == Entity.Null || !_em.Exists(_zoneEntity))
            return;

        if (_worldTarget != null)
        {
            Vector3 pos = _worldTarget.position + Vector3.up * _offsetY;
            Vector3 screen = Camera.main.WorldToScreenPoint(pos);
            transform.position = screen;
        }

        if (_em.HasComponent<ZoneTriggerComponent>(_zoneEntity))
        {
            var zone = _em.GetComponentData<ZoneTriggerComponent>(_zoneEntity);
            if (_lockIcon != null)
                _lockIcon.enabled = zone.isLocked;

            if (_label != null && zone.isFinal)
                _label.text = "Base";

            if (_progressFill != null)
            {
                Color c = _neutralColor;
                int teamOwner = zone.teamOwner;
                if (TryGetPlayerTeam(out var playerTeam))
                {
                    if (teamOwner == (int)playerTeam)
                        c = _playerColor;
                    else if (teamOwner != 0)
                        c = _enemyColor;
                }
                else
                {
                    if (teamOwner == (int)Team.TeamA)
                        c = _playerColor;
                    else if (teamOwner == (int)Team.TeamB)
                        c = _enemyColor;
                }
                _progressFill.color = c;
            }
        }

        if (_em.HasComponent<CapturePointProgressComponent>(_zoneEntity))
        {
            var prog = _em.GetComponentData<CapturePointProgressComponent>(_zoneEntity);
            if (_progressFill != null)
                _progressFill.fillAmount = prog.captureProgress / 100f;
            if (_contestedGroup != null)
                _contestedGroup.alpha = prog.isContested ? 1f : 0f;
        }
    }

    bool TryGetPlayerTeam(out Team team)
    {
        team = Team.None;
        var q = _em.CreateEntityQuery(ComponentType.ReadOnly<DataContainerComponent>());
        if (q.IsEmptyIgnoreFilter)
            return false;
        var data = q.GetSingleton<DataContainerComponent>();
        team = (Team)data.teamID;
        return true;
    }
}
