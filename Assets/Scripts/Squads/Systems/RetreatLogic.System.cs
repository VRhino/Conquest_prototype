using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Moves squads in the <see cref="SquadFSMState.Retreating"/> state towards a
/// safe location and removes them once the retreat completes.
/// For swap-originated retreats, persists alive-unit count into the hero's
/// <see cref="InactiveSquadElement"/> buffer before destruction.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(SquadFSMSystem))]
public partial class RetreatLogicSystem : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<MatchStateComponent>();
    }

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
                // If this squad retreated due to a swap, persist alive-unit count
                if (SystemAPI.HasComponent<SquadRetreatingFromSwapTag>(entity))
                {
                    var swapTag = SystemAPI.GetComponent<SquadRetreatingFromSwapTag>(entity);
                    Entity heroEntity = swapTag.heroEntity;

                    if (SystemAPI.Exists(heroEntity) && SystemAPI.HasBuffer<InactiveSquadElement>(heroEntity))
                    {
                        // Count alive units
                        int aliveCount = 0;
                        for (int u = 0; u < units.Length; u++)
                        {
                            Entity unitEntity = units[u].Value;
                            if (SystemAPI.Exists(unitEntity) && !SystemAPI.HasComponent<IsDeadComponent>(unitEntity))
                            {
                                aliveCount++;
                            }
                        }

                        // Update the corresponding InactiveSquadElement
                        var inactiveBuffer = SystemAPI.GetBuffer<InactiveSquadElement>(heroEntity);
                        for (int b = 0; b < inactiveBuffer.Length; b++)
                        {
                            if (inactiveBuffer[b].squadId == swapTag.squadId)
                            {
                                var elem = inactiveBuffer[b];
                                elem.aliveUnits = aliveCount;
                                elem.isEliminated = aliveCount == 0;
                                inactiveBuffer[b] = elem;
                                break;
                            }
                        }
                    }
                }

                // Destroy all unit entities before destroying the squad
                for (int u = 0; u < units.Length; u++)
                {
                    Entity unitEntity = units[u].Value;
                    if (SystemAPI.Exists(unitEntity))
                    {
                        ecb.DestroyEntity(unitEntity);
                    }
                }

                ecb.DestroyEntity(entity);
            }
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
