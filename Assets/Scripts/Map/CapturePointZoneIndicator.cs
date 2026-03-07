using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Visual indicator showing the radius of influence of a capture point.
/// Changes color based on team ownership and capture progress relative to the local player.
/// Receives its zone entity from the parent CapturePointSetup.
/// </summary>
public class CapturePointZoneIndicator : MonoBehaviour
{
    [Header("Colors")]
    [SerializeField] Color _neutralColor = new Color(0.5f, 0.5f, 0.5f, 0.4f);
    [SerializeField] Color _alliedColor = new Color(0.2f, 0.4f, 1f, 0.4f);
    [SerializeField] Color _enemyColor = new Color(1f, 0.2f, 0.2f, 0.4f);

    [Header("Fallback")]
    [SerializeField] float _fallbackRadius = 10f;

    Renderer _renderer;
    MaterialPropertyBlock _mpb;
    EntityManager _em;
    Entity _zoneEntity;
    EntityQuery _dcQuery;
    int _cachedColorHash;
    bool _initialized;

    public void Initialize(int zoneId, Entity zoneEntity, bool isLocked)
    {
        _zoneEntity = zoneEntity;
        _initialized = true;
        gameObject.SetActive(!isLocked);
    }

    void Start()
    {
        _renderer = GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();
        _em = World.DefaultGameObjectInjectionWorld.EntityManager;
        _dcQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<DataContainerComponent>());

        if (!_initialized)
            FindZoneEntityByProximity();

        SetScaleFromRadius();
    }

    void OnDestroy()
    {
        if (World.DefaultGameObjectInjectionWorld != null && World.DefaultGameObjectInjectionWorld.IsCreated)
            _dcQuery.Dispose();
    }

    void FindZoneEntityByProximity()
    {
        var query = _em.CreateEntityQuery(
            ComponentType.ReadOnly<ZoneTriggerComponent>(),
            ComponentType.ReadOnly<LocalTransform>());
        using var ents = query.ToEntityArray(Allocator.Temp);

        float closestDist = float.MaxValue;
        float3 myPos = transform.position;

        foreach (var ent in ents)
        {
            var zone = _em.GetComponentData<ZoneTriggerComponent>(ent);
            if (zone.zoneType != ZoneType.Capture) continue;

            var pos = _em.GetComponentData<LocalTransform>(ent).Position;
            float dist = math.distancesq(myPos, pos);
            if (dist < closestDist)
            {
                closestDist = dist;
                _zoneEntity = ent;
            }
        }
    }

    void SetScaleFromRadius()
    {
        float radius = _fallbackRadius;
        if (_zoneEntity != Entity.Null && _em.HasComponent<ZoneTriggerComponent>(_zoneEntity))
        {
            var zone = _em.GetComponentData<ZoneTriggerComponent>(_zoneEntity);
            radius = zone.radius;
        }
        float diameter = radius * 2f;
        transform.localScale = new Vector3(diameter, diameter, diameter);
    }

    void Update()
    {
        if (_zoneEntity == Entity.Null || !_em.Exists(_zoneEntity)) return;

        Team playerTeam = Team.None;
        if (!_dcQuery.IsEmptyIgnoreFilter)
        {
            int teamID = _dcQuery.GetSingleton<DataContainerComponent>().teamID;
            if (teamID != 0) playerTeam = (Team)teamID;
        }

        var zone = _em.GetComponentData<ZoneTriggerComponent>(_zoneEntity);

        var progress = _em.GetComponentData<CapturePointProgressComponent>(_zoneEntity);
        Color color = ResolveColor(zone.teamOwner, progress.isBeingCaptured, playerTeam);

        int hash = color.GetHashCode();
        if (hash != _cachedColorHash)
        {
            _cachedColorHash = hash;
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_BaseColor", color);
            _renderer.SetPropertyBlock(_mpb);
        }
    }

    Color ResolveColor(int teamOwner, bool isBeingCaptured, Team playerTeam)
    {
        if (isBeingCaptured) return _neutralColor;
        if (teamOwner == 0) return _neutralColor;
        if ((Team)teamOwner == playerTeam) return _alliedColor;
        return _enemyColor;
    }
}
