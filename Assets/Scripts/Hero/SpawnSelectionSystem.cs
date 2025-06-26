using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Processes <see cref="SpawnSelectionRequest"/> components to update the hero's
/// chosen spawn point. This system is expected to run during the preparation
/// phase before combat starts.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class SpawnSelectionSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (request, spawn, entity) in SystemAPI
                     .Query<RefRO<SpawnSelectionRequest>, RefRW<HeroSpawnComponent>>()
                     .WithAll<IsLocalPlayer>()
                     .WithEntityAccess())
        {
            spawn.ValueRW.spawnId = request.ValueRO.spawnId;
            spawn.ValueRW.hasSpawned = false;
            ecb.RemoveComponent<SpawnSelectionRequest>(entity);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
