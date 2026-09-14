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
[UpdateAfter(typeof(SquadNavigationSystem))]
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

            bool reached = SquadNavigationSystem.HasArrived(EntityManager, units,
                retreat.ValueRO.retreatTarget, nav.ValueRO.arrivalThreshold);

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
                if (SystemAPI.HasComponent<SquadOwnerDeathRetreatComponent>(entity))
                    PersistOwnerDeathRetreat(entity, units, ecb);

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

    private void PersistOwnerDeathRetreat(Entity squad, DynamicBuffer<SquadUnitElement> units, EntityCommandBuffer ecb)
    {
        var em = EntityManager;
        if (!em.HasComponent<SquadOwnerComponent>(squad) || !em.HasComponent<SquadInstanceComponent>(squad)) return;
        Entity hero = em.GetComponentData<SquadOwnerComponent>(squad).hero;
        if (!em.Exists(hero)) return;
        var instance = em.GetComponentData<SquadInstanceComponent>(squad);
        var entry = new InactiveSquadElement { squadId = instance.id,
            totalUnits = math.max(instance.initialUnitCount, units.Length) };
        for (int i = 0; i < units.Length; i++)
            if (em.Exists(units[i].Value) && !em.HasComponent<IsDeadComponent>(units[i].Value)) entry.aliveUnits++;
        entry.isEliminated = entry.aliveUnits == 0;
        Entity mapOwner = hero;
        if (!em.HasBuffer<SquadIdMapElement>(hero) && em.HasComponent<IsLocalPlayer>(hero)
            && SystemAPI.TryGetSingletonEntity<DataContainerComponent>(out var container)) mapOwner = container;
        if (em.HasBuffer<SquadIdMapElement>(mapOwner))
            foreach (var mapping in em.GetBuffer<SquadIdMapElement>(mapOwner))
                if (mapping.squadId == instance.id) { entry.baseSquadID = mapping.baseSquadID; break; }
        if (entry.baseSquadID.IsEmpty && em.HasComponent<HeroSquadSelectionComponent>(hero))
        {
            var selection = em.GetComponentData<HeroSquadSelectionComponent>(hero);
            if (selection.instanceId == instance.id && em.HasComponent<SquadDataIDComponent>(selection.squadDataEntity))
                entry.baseSquadID = em.GetComponentData<SquadDataIDComponent>(selection.squadDataEntity).id;
        }
        if (em.HasBuffer<InactiveSquadElement>(hero))
        {
            var reserves = em.GetBuffer<InactiveSquadElement>(hero);
            for (int i = reserves.Length - 1; i >= 0; i--)
                if (reserves[i].squadId == instance.id) reserves.RemoveAt(i);
            reserves.Add(entry);
        }
        else ecb.AddBuffer<InactiveSquadElement>(hero).Add(entry);

        // A different/new squad reference must never be removed by old retirement cleanup.
        if (em.HasComponent<HeroSquadReference>(hero) && em.GetComponentData<HeroSquadReference>(hero).squad == squad)
            ecb.RemoveComponent<HeroSquadReference>(hero);
    }
}
