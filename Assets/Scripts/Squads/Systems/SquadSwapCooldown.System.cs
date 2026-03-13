using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Ticks down <see cref="SquadSwapCooldownComponent"/> on hero entities and
/// removes it when the cooldown expires.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class SquadSwapCooldownSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (cooldown, entity) in SystemAPI
                     .Query<RefRW<SquadSwapCooldownComponent>>()
                     .WithEntityAccess())
        {
            cooldown.ValueRW.remainingTime -= dt;
            if (cooldown.ValueRO.remainingTime <= 0f)
            {
                ecb.RemoveComponent<SquadSwapCooldownComponent>(entity);
            }
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
