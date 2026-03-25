using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Visual indicator showing the radius of influence of a supply point.
/// Changes color based on team ownership relative to the local player.
/// Receives its zone entity from the parent SupplyPointSetup.
/// </summary>
public class SupplyPointZoneIndicator : MonoBehaviour
{
    [Header("Colors")]
    [SerializeField] Color _neutralColor = new Color(0.5f, 0.5f, 0.5f, 0.4f);
    [SerializeField] Color _alliedColor = new Color(0.2f, 0.4f, 1f, 0.4f);
    [SerializeField] Color _enemyColor = new Color(1f, 0.2f, 0.2f, 0.4f);

    [Header("Fallback")]
    [SerializeField] float _fallbackRadius = 10f;

    [Header("Glow")]
    [SerializeField] Renderer _glowRenderer;

    Renderer _renderer;
    MaterialPropertyBlock _mpb;
    MaterialPropertyBlock _glowMpb;
    EntityManager _em;
    Entity _zoneEntity;
    Team _playerTeam;
    int _cachedColorHash;
    bool _initialized;

    public void Initialize(int zoneId, Entity zoneEntity)
    {
        _zoneEntity = zoneEntity;
        _initialized = true;
    }

    void Start()
    {
        _renderer = GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();
        _em = World.DefaultGameObjectInjectionWorld.EntityManager;
        CachePlayerTeam();

        if (!_initialized)
            FindZoneEntityByProximity();

        SetScaleFromRadius();
        InitGlowRenderer();
    }

    void InitGlowRenderer()
    {
        if (_glowRenderer == null) return;
        _glowMpb = new MaterialPropertyBlock();
        _glowMpb.SetColor("_BaseColor", _neutralColor);
        _glowRenderer.SetPropertyBlock(_glowMpb);
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
            if (zone.zoneType != ZoneType.Supply) continue;

            var pos = _em.GetComponentData<LocalTransform>(ent).Position;
            float dist = math.distancesq(myPos, pos);
            if (dist < closestDist)
            {
                closestDist = dist;
                _zoneEntity = ent;
            }
        }
    }

    void CachePlayerTeam()
    {
        var q = _em.CreateEntityQuery(ComponentType.ReadOnly<DataContainerComponent>());
        if (!q.IsEmptyIgnoreFilter)
            _playerTeam = (Team)q.GetSingleton<DataContainerComponent>().teamID;
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

        var zone = _em.GetComponentData<ZoneTriggerComponent>(_zoneEntity);
        var supply = _em.GetComponentData<SupplyPointComponent>(_zoneEntity);
        Color color = ResolveColor(zone.teamOwner, supply.isCapturing);

        float captureProgress = supply.isCapturing ? supply.captureProgress / 100f : 0f;
        int hash = color.GetHashCode() * 31 + captureProgress.GetHashCode();
        if (hash != _cachedColorHash)
        {
            _cachedColorHash = hash;
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_BaseColor", color);
            _mpb.SetFloat("_CaptureProgress", captureProgress);
            _renderer.SetPropertyBlock(_mpb);

            if (_glowRenderer != null)
            {
                _glowRenderer.GetPropertyBlock(_glowMpb);
                _glowMpb.SetColor("_BaseColor", color);
                _glowRenderer.SetPropertyBlock(_glowMpb);
            }
        }
    }

    Color ResolveColor(int teamOwner, bool isCapturing)
    {
        if (isCapturing) return _neutralColor;
        if (teamOwner == 0) return _neutralColor;
        if ((Team)teamOwner == _playerTeam) return _alliedColor;
        return _enemyColor;
    }
}
