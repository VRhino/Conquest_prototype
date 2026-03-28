using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Ticks the channeling timer for squad swaps. Verifies cancellation conditions
/// each frame and, upon completion, adds <see cref="SquadSwapExecuteTag"/> to trigger
/// the actual swap.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(SquadSwapSystem))]
public partial class SquadSwapChannelingSystem : SystemBase
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

        foreach (var (channeling, life, heroTransform, team, entity) in SystemAPI
                     .Query<RefRW<SquadSwapChannelingComponent>,
                            RefRO<HeroLifeComponent>,
                            RefRO<LocalTransform>,
                            RefRO<TeamComponent>>()
                     .WithEntityAccess())
        {
            // Cancel if hero died
            if (!life.ValueRO.isAlive)
            {
                ecb.RemoveComponent<SquadSwapChannelingComponent>(entity);
                continue;
            }

            // Validate zone conditions
            bool zoneValid = false;
            foreach (var (zone, supply, zoneTransform) in SystemAPI
                         .Query<RefRO<ZoneTriggerComponent>,
                                RefRO<SupplyPointComponent>,
                                RefRO<LocalTransform>>())
            {
                if (zone.ValueRO.zoneId != channeling.ValueRO.zoneId)
                    continue;

                // Zone must still be owned by hero's team
                if (zone.ValueRO.teamOwner != (int)team.ValueRO.value)
                    break;

                // Zone must not be contested
                if (supply.ValueRO.isContested)
                    break;

                // Hero must still be within zone radius
                float radiusSq = zone.ValueRO.radius * zone.ValueRO.radius;
                float distSq = math.distancesq(heroTransform.ValueRO.Position, zoneTransform.ValueRO.Position);
                if (distSq > radiusSq)
                    break;

                zoneValid = true;
                break;
            }

            if (!zoneValid)
            {
                // Cancel channeling — no cooldown
                ecb.RemoveComponent<SquadSwapChannelingComponent>(entity);
                continue;
            }

            // Tick timer
            channeling.ValueRW.timer += dt;

            if (channeling.ValueRO.timer >= channeling.ValueRO.duration)
            {
                // Channeling complete — trigger execution
                ecb.AddComponent(entity, new SquadSwapExecuteTag
                {
                    newSquadId = channeling.ValueRO.targetSquadId
                });
                ecb.AddComponent(entity, new SquadSwapCooldownComponent
                {
                    remainingTime = 10f
                });
                ecb.RemoveComponent<SquadSwapChannelingComponent>(entity);
            }
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
