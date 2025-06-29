using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Moves each unit of a squad towards its assigned formation slot relative to
/// the squad leader.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(FormationSystem))]
public partial class UnitFollowFormationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Dependency.Complete();
        const float moveSpeed = 5f;
        const float stoppingDistanceSq = 0.04f; // ~0.2m

        float dt = SystemAPI.Time.DeltaTime;

        var slotLookup = GetComponentLookup<UnitFormationSlotComponent>(true);
        var targetLookup = GetComponentLookup<UnitLocalTargetComponent>();
        var transformLookup = GetComponentLookup<LocalTransform>();
        var ownerLookup = GetComponentLookup<SquadOwnerComponent>(true);

        foreach (var (units, entity) in SystemAPI.Query<DynamicBuffer<SquadUnitElement>>().WithEntityAccess())
        {
            if (units.Length == 0)
                continue;

            if (!ownerLookup.HasComponent(entity))
                continue;

            Entity leader = ownerLookup[entity].hero;
            if (!transformLookup.HasComponent(leader))
                continue;

            float3 leaderPos = transformLookup[leader].Position;

            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i].Value;
                if (!slotLookup.HasComponent(unit) ||
                    !transformLookup.HasComponent(unit))
                    continue;

                float3 desired = leaderPos + slotLookup[unit].relativeOffset;
                float3 current = transformLookup[unit].Position;
                float3 diff = desired - current;
                float distSq = math.lengthsq(diff);

                // UnityEngine.Debug.Log($"[UnitFollowFormationSystem] unit: {unit}, leader: {leader}, offset: {slotLookup[unit].relativeOffset}, desired: {desired}, current: {current}, distSq: {distSq}");

                if (distSq > stoppingDistanceSq)
                {
                    float3 step = math.normalizesafe(diff) * moveSpeed * dt;
                    if (math.lengthsq(step) > distSq)
                        step = diff;
                    current += step;
                    var t = transformLookup[unit];
                    t.Position = current;
                    transformLookup[unit] = t;
                }

                if (targetLookup.HasComponent(unit))
                {
                    var target = targetLookup[unit];
                    target.targetPosition = desired;
                    targetLookup[unit] = target;
                }
            }
        }
    }
}
