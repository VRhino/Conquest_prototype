using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Adjusts local target positions of squad units so they keep a minimum
/// separation from each other and avoid visual overlap.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(FormationSystem))]
public partial class UnitSpacingSystem : SystemBase
{
    protected override void OnUpdate()
    {
        const float maxPush = 0.5f;

        var localTargetLookup = GetComponentLookup<UnitLocalTargetComponent>();
        var spacingLookup = GetComponentLookup<UnitSpacingComponent>(true);
        var transformLookup = GetComponentLookup<LocalTransform>(true);

        foreach (var units in SystemAPI.Query<DynamicBuffer<SquadUnitElement>>())
        {
            int count = units.Length;
            for (int i = 0; i < count; i++)
            {
                Entity entityA = units[i].Value;
                if (!localTargetLookup.HasComponent(entityA) ||
                    !spacingLookup.HasComponent(entityA) ||
                    !transformLookup.HasComponent(entityA))
                    continue;

                float3 posA = transformLookup[entityA].Position;
                var spacing = spacingLookup[entityA];
                float3 offset = float3.zero;

                for (int j = 0; j < count; j++)
                {
                    if (i == j) continue;

                    Entity entityB = units[j].Value;
                    if (!transformLookup.HasComponent(entityB))
                        continue;

                    float3 posB = transformLookup[entityB].Position;
                    float3 diff = posA - posB;
                    float distSq = math.lengthsq(diff);
                    float minDistSq = spacing.minDistance * spacing.minDistance;
                    if (distSq < minDistSq && distSq > 1e-6f)
                    {
                        float dist = math.sqrt(distSq);
                        float3 dir = diff / dist;
                        float push = (spacing.minDistance - dist) * spacing.repelForce;
                        offset += dir * push;
                    }
                }

                if (!math.all(offset == float3.zero))
                {
                    float len = math.length(offset);
                    if (len > maxPush)
                        offset = offset * (maxPush / len);

                    var target = localTargetLookup[entityA];
                    target.targetPosition += offset;
                    localTargetLookup[entityA] = target;
                }
            }
        }
    }
}
