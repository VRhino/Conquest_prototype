using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Initializes a capture point at runtime: creates the ECS entity and
/// auto-assigns a unique zoneId. Place on the root of the CapturePoint prefab.
/// </summary>
public class CapturePointSetup : MonoBehaviour
{
    [Header("Zone Configuration")]
    [SerializeField] float _radius = 10f;
    [SerializeField] float _captureSpeed = 1f;
    [SerializeField] Team _initialTeam = Team.TeamB;

    [Header("Capture Point Options")]
    [SerializeField] bool _isLocked;
    [SerializeField] bool _isFinal;
    [SerializeField] CapturePointSetup _requiredCapturePoint;

    [Header("Runtime Info (Play Mode)")]
    [SerializeField] int _zoneId;

    static int _nextZoneId = 100;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _nextZoneId = 100;

    public int ZoneId { get; private set; }
    public Entity ZoneEntity { get; private set; }

    CapturePointZoneIndicator _indicator;
    EntityManager _em;

    void Awake()
    {
        ZoneId = _nextZoneId++;
        _zoneId = ZoneId;
    }

    void Start()
    {
        _em = World.DefaultGameObjectInjectionWorld.EntityManager;
        var em = _em;
        ZoneEntity = em.CreateEntity();

        int teamInt = (int)_initialTeam;

        em.AddComponentData(ZoneEntity, new ZoneTriggerComponent
        {
            zoneId = ZoneId,
            zoneType = ZoneType.Capture,
            teamOwner = teamInt,
            isActive = true,
            radius = _radius,
            isLocked = _isLocked,
            isFinal = _isFinal
        });

        em.AddComponentData(ZoneEntity, new CapturePointProgressComponent
        {
            captureProgress = 0f,
            captureSpeed = _captureSpeed,
            capturingTeam = 0,
            isContested = false
        });

        em.AddComponentData(ZoneEntity, LocalTransform.FromPosition(
            new float3(transform.position.x, transform.position.y, transform.position.z)));

        em.AddComponentData(ZoneEntity, new CapturePointTag());

        if (_requiredCapturePoint != null)
        {
            em.AddComponentData(ZoneEntity, new ZoneLinkComponent
            {
                requiredZoneId = _requiredCapturePoint.ZoneId
            });
        }

        _indicator = GetComponentInChildren<CapturePointZoneIndicator>(true);

        if (_indicator != null) _indicator.Initialize(ZoneId, ZoneEntity, _isLocked);
    }

    void Update()
    {
        if (ZoneEntity == Entity.Null || !_em.Exists(ZoneEntity) || _indicator == null) return;
        var zone = _em.GetComponentData<ZoneTriggerComponent>(ZoneEntity);
        bool shouldBeActive = !zone.isLocked;
        if (_indicator.gameObject.activeSelf != shouldBeActive)
        {
            _indicator.gameObject.SetActive(shouldBeActive);
        }
    }
}
