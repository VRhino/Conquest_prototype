using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Monitors navigation completion. UnitNavMeshSystem owns movement destinations.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(GridFormationUpdateSystem))]
public partial class SquadNavigationSystem : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<MatchStateComponent>();
    }

    protected override void OnUpdate()
    {

        foreach (var (nav, state, units, entity) in SystemAPI
                     .Query<RefRW<SquadNavigationComponent>,
                            RefRO<SquadStateComponent>,
                            DynamicBuffer<SquadUnitElement>>()
                     .WithEntityAccess())
        {
            if (!nav.ValueRO.isNavigating || units.Length == 0)
                continue;

            if (HasArrived(EntityManager, units, nav.ValueRO.targetPosition, nav.ValueRO.arrivalThreshold))
                nav.ValueRW.isNavigating = false;
        }
    }

    public static bool HasArrived(EntityManager em, DynamicBuffer<SquadUnitElement> units, float3 fallback, float threshold)
    {
        for (int i = 0; i < units.Length; i++)
        {
            Entity unit = units[i].Value;
            if (!em.Exists(unit) || em.HasComponent<IsDeadComponent>(unit)) continue;
            if (!em.HasComponent<LocalTransform>(unit)) return false;
            float3 requested = em.HasComponent<UnitTargetPositionComponent>(unit)
                ? em.GetComponentData<UnitTargetPositionComponent>(unit).position : fallback;
            float3 destination = GetEffectiveFormationDestination(em, unit, requested);
            if (math.distancesq(em.GetComponentData<LocalTransform>(unit).Position, destination)
                > math.square(math.max(0, threshold))) return false;
        }
        return true;
    }

    public static float3 GetEffectiveFormationDestination(EntityManager em, Entity unit, float3 requested)
    {
        if (!em.HasComponent<NavAgentComponent>(unit)) return requested;
        var navigation = em.GetComponentData<NavAgentComponent>(unit);
        return navigation.hasEffectiveFormationDestination
            && math.distancesq(navigation.lastFormationRequest, requested) <= 0.0001f
            ? navigation.effectiveFormationDestination
            : requested;
    }
}
