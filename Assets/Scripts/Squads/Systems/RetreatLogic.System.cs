using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Moves squads in the <see cref="SquadFSMState.Retreating"/> state towards a
/// safe location and removes them once the retreat completes.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(SquadFSMSystem))]
public partial class RetreatLogicSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;

        var transformLookup = GetComponentLookup<LocalTransform>(true);
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (state, retreat, nav, units, entity) in SystemAPI
                     .Query<RefRO<SquadStateComponent>,
                            RefRW<RetreatComponent>,
                            RefRW<SquadNavigationComponent>,
                            DynamicBuffer<SquadUnitElement>>()
                     .WithEntityAccess())
        {
            if (state.ValueRO.currentState != SquadFSMState.Retreating)
                continue;

            retreat.ValueRW.retreatTimer += dt;

            nav.ValueRW.targetPosition = retreat.ValueRO.retreatTarget;
            nav.ValueRW.isNavigating = true;
            if (nav.ValueRW.arrivalThreshold <= 0f)
                nav.ValueRW.arrivalThreshold = 0.5f;

            bool reached = false;
            if (units.Length > 0)
            {
                Entity leader = units[0].Value;
                if (SystemAPI.Exists(leader) && transformLookup.HasComponent(leader))
                {
                    float3 pos = transformLookup[leader].Position;
                    float distSq = math.distancesq(pos, retreat.ValueRO.retreatTarget);
                    reached = distSq <= nav.ValueRO.arrivalThreshold * nav.ValueRO.arrivalThreshold;
                }
            }

            if (reached || retreat.ValueRO.retreatTimer >= retreat.ValueRO.retreatDuration)
            {
                ecb.DestroyEntity(entity);
            }
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
