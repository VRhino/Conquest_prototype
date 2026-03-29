using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Server-side system that validates <see cref="SquadSwapRequest"/> components
/// and initiates channeling if the hero is at a safe allied supply point.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class SquadSwapSystem : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<MatchStateComponent>();
    }

    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (request, life, team, entity) in SystemAPI
                     .Query<RefRO<SquadSwapRequest>,
                            RefRO<HeroLifeComponent>,
                            RefRO<TeamComponent>>()
                     .WithEntityAccess())
        {
            bool remove = true;

            if (!life.ValueRO.isAlive)
            {
                ecb.RemoveComponent<SquadSwapRequest>(entity);
                continue;
            }

            // Reject if already channeling or on cooldown
            if (SystemAPI.HasComponent<SquadSwapChannelingComponent>(entity) ||
                SystemAPI.HasComponent<SquadSwapCooldownComponent>(entity))
            {
                ecb.RemoveComponent<SquadSwapRequest>(entity);
                continue;
            }

            // Reject if current squad is in combat
            if (SystemAPI.HasComponent<HeroSquadReference>(entity))
            {
                Entity squadEntity = SystemAPI.GetComponent<HeroSquadReference>(entity).squad;
                if (SystemAPI.HasComponent<SquadAIComponent>(squadEntity) &&
                    SystemAPI.GetComponent<SquadAIComponent>(squadEntity).isInCombat)
                {
                    ecb.RemoveComponent<SquadSwapRequest>(entity);
                    continue;
                }
            }

            // Validate zone
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

            // Start channeling
            ecb.AddComponent(entity, new SquadSwapChannelingComponent
            {
                targetSquadId = request.ValueRO.newSquadId,
                zoneId = request.ValueRO.zoneId,
                timer = 0f,
                duration = 1.0f
            });
            ecb.RemoveComponent<SquadSwapRequest>(entity);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
