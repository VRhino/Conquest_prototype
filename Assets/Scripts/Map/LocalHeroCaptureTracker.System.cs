using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Checks each frame whether the local player's hero is inside any active capture
/// or supply zone and writes the result to the <see cref="LocalHeroCaptureStateComponent"/> singleton.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class LocalHeroCaptureTrackerSystem : SystemBase
{
    private Entity _stateEntity;

    protected override void OnCreate()
    {
        _stateEntity = EntityManager.CreateEntity();
        EntityManager.AddComponentData(_stateEntity, new LocalHeroCaptureStateComponent());
    }

    protected override void OnUpdate()
    {
        // --- Find local hero ---
        Entity heroEntity = Entity.Null;
        float3 heroPos = float3.zero;

        foreach (var (transform, _, entity) in
                 SystemAPI.Query<RefRO<LocalTransform>, RefRO<IsLocalPlayer>>()
                          .WithEntityAccess())
        {
            heroEntity = entity;
            heroPos = transform.ValueRO.Position;
            break;
        }

        if (heroEntity == Entity.Null)
        {
            SystemAPI.SetComponent(_stateEntity, new LocalHeroCaptureStateComponent { isInZone = false });
            return;
        }

        // --- Check capture zones ---
        foreach (var (zone, progress, zTransform) in
                 SystemAPI.Query<RefRO<ZoneTriggerComponent>,
                                 RefRO<CapturePointProgressComponent>,
                                 RefRO<LocalTransform>>())
        {
            if (!zone.ValueRO.isActive)
                continue;

            float radiusSq = zone.ValueRO.radius * zone.ValueRO.radius;
            if (math.distancesq(heroPos, zTransform.ValueRO.Position) > radiusSq)
                continue;

            SystemAPI.SetComponent(_stateEntity, new LocalHeroCaptureStateComponent
            {
                isInZone = true,
                captureProgress = progress.ValueRO.captureProgress,
                isContested = progress.ValueRO.isContested
            });
            return;
        }

        // --- Check supply zones ---
        foreach (var (zone, supply, zTransform) in
                 SystemAPI.Query<RefRO<ZoneTriggerComponent>,
                                 RefRO<SupplyPointComponent>,
                                 RefRO<LocalTransform>>())
        {
            if (!zone.ValueRO.isActive || !supply.ValueRO.isCapturing)
                continue;

            float radiusSq = zone.ValueRO.radius * zone.ValueRO.radius;
            if (math.distancesq(heroPos, zTransform.ValueRO.Position) > radiusSq)
                continue;

            SystemAPI.SetComponent(_stateEntity, new LocalHeroCaptureStateComponent
            {
                isInZone = true,
                captureProgress = supply.ValueRO.captureProgress,
                isContested = supply.ValueRO.isContested
            });
            return;
        }

        // --- Not in any zone ---
        SystemAPI.SetComponent(_stateEntity, new LocalHeroCaptureStateComponent { isInZone = false });
    }
}
