using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Server-side system that validates <see cref="SquadSwapRequest"/> components
/// and performs the squad change if the hero is at a safe allied supply point.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class SquadSwapSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var zoneLookup = GetComponentLookup<ZoneTriggerComponent>(true);
        var supplyLookup = GetComponentLookup<SupplyPointComponent>(true);
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (request, life, team, entity) in SystemAPI
                     .Query<RefRO<SquadSwapRequest>,
                            RefRO<HeroLifeComponent>,
                            RefRO<TeamComponent>>()
                     .WithEntityAccess())
        {
            if (!life.ValueRO.isAlive)
            {
                ecb.RemoveComponent<SquadSwapRequest>(entity);
                continue;
            }

            bool valid = false;
            foreach (var (zone, supply) in SystemAPI
                         .Query<RefRO<ZoneTriggerComponent>, RefRO<SupplyPointComponent>>())
            {
                if (zone.ValueRO.zoneId != request.ValueRO.zoneId)
                    continue;

                if (zone.ValueRO.teamOwner != (int)team.ValueRO.value)
                    break;

                if (!supply.ValueRO.isContested)
                    valid = true;
                break;
            }

            if (!valid)
            {
                ecb.RemoveComponent<SquadSwapRequest>(entity);
                continue;
            }

            Entity evt = ecb.CreateEntity();
            ecb.AddComponent(evt, new SquadChangeEvent { newSquadId = request.ValueRO.newSquadId });
            ecb.RemoveComponent<SquadSwapRequest>(entity);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
