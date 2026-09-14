using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>Connects owner life to a complete, one-shot squad retirement request.</summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(HeroRespawnSystem))]
[UpdateBefore(typeof(SquadOrderSystem))]
[UpdateBefore(typeof(SquadSwapExecutionSystem))]
[UpdateBefore(typeof(SquadStatusUpdateSystem))]
public partial class SquadOwnerDeathRetreatSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<MatchStateComponent>();
        RequireForUpdate<SquadSpawnConfigComponent>();
    }

    protected override void OnUpdate()
    {
        var config = SystemAPI.GetSingleton<SquadSpawnConfigComponent>();
        using var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (owner, team, state, squad) in SystemAPI.Query<RefRO<SquadOwnerComponent>,
            RefRO<TeamComponent>, RefRW<SquadStateComponent>>().WithEntityAccess())
        {
            if (!SystemAPI.HasComponent<HeroLifeComponent>(owner.ValueRO.hero)) continue;
            bool alive = SystemAPI.GetComponent<HeroLifeComponent>(owner.ValueRO.hero).isAlive;
            state.ValueRW.lastOwnerAlive = alive;
            bool committed = SystemAPI.HasComponent<SquadOwnerDeathRetreatComponent>(squad);
            // Never replace an existing swap retirement or restart its timer.
            if (SystemAPI.HasComponent<RetreatComponent>(squad)
                || SystemAPI.HasComponent<SquadRetreatingFromSwapTag>(squad)
                || (alive && !committed)) continue;
            if (!committed) ecb.AddComponent<SquadOwnerDeathRetreatComponent>(squad);
            state.ValueRW.currentState = SquadFSMState.Retreating;
            state.ValueRW.transitionTo = SquadFSMState.Retreating;
            state.ValueRW.retreatTriggered = true;
            ecb.RemoveComponent<IsLocalSquadActive>(squad);

            bool found = false;
            float3 target = default;
            foreach (var point in SystemAPI.Query<RefRO<SpawnPointComponent>>())
                if (point.ValueRO.isActive && point.ValueRO.teamID == (int)team.ValueRO.value)
                { target = point.ValueRO.position; found = true; break; }
            if (!found) continue; // Persist request; do not fabricate a world-origin destination.
            ecb.AddComponent(squad, new RetreatComponent { retreatTarget = target,
                retreatDuration = math.max(0, config.ownerDeathRetreatDuration) });
            var navigation = new SquadNavigationComponent { targetPosition = target, isNavigating = true,
                arrivalThreshold = config.retreatArrivalThreshold };
            if (SystemAPI.HasComponent<SquadNavigationComponent>(squad)) ecb.SetComponent(squad, navigation);
            else ecb.AddComponent(squad, navigation);
        }
        ecb.Playback(EntityManager);
    }
}
