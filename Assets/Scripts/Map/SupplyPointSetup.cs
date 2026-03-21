using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Initializes a supply point at runtime: creates the ECS entity and
/// auto-assigns a unique zoneId. Place on the root of the SupplyPoint prefab.
/// </summary>
public class SupplyPointSetup : MonoBehaviour
{
    [Header("Zone Configuration")]
    [SerializeField] float _radius = 10f;
    [SerializeField] float _captureSpeed = 1f;
    [SerializeField] Team _initialTeam = Team.None;

    [Header("Runtime Info (Play Mode)")]
    [SerializeField] int _zoneId;

    static int _nextZoneId = 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _nextZoneId = 1;

    public int ZoneId { get; private set; }
    public Entity ZoneEntity { get; private set; }

    void Start()
    {
        ZoneId = _nextZoneId++;
        _zoneId = ZoneId;

        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        ZoneEntity = em.CreateEntity();
#if UNITY_EDITOR
        em.SetName(ZoneEntity, $"SupplyPoint_{ZoneId}_{gameObject.name}");
#endif

        int teamInt = (int)_initialTeam;

        em.AddComponentData(ZoneEntity, new ZoneTriggerComponent
        {
            zoneId = ZoneId,
            zoneType = ZoneType.Supply,
            teamOwner = teamInt,
            isActive = true,
            radius = _radius,
            isLocked = false,
            isFinal = false
        });

        em.AddComponentData(ZoneEntity, new SupplyPointComponent
        {
            captureProgress = 0f,
            captureSpeed = _captureSpeed,
            currentTeam = teamInt,
            isContested = false
        });

        em.AddComponentData(ZoneEntity, LocalTransform.FromPosition(
            new float3(transform.position.x, transform.position.y, transform.position.z)));

        em.AddComponentData(ZoneEntity, new SupplyPointTag());

        var indicator = GetComponentInChildren<SupplyPointZoneIndicator>();
        if (indicator != null)
            indicator.Initialize(ZoneId, ZoneEntity);

        Debug.Log($"[SupplyPointSetup] Created supply point zoneId={ZoneId} at {transform.position}");
    }
}
