using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

/// <summary>
/// Places the local hero at the selected spawn point at the start of the match
/// and after a respawn.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class HeroSpawnSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var spawnPoints = SystemAPI.Query<RefRO<SpawnPointComponent>>().ToComponentDataArray<SpawnPointComponent>(Allocator.Temp);
        float dt = SystemAPI.Time.DeltaTime;

        foreach (var (life, spawnData, team, transform) in
                 SystemAPI.Query<RefRO<HeroLifeComponent>,
                                 RefRW<HeroSpawnComponent>,
                                 RefRO<TeamComponent>,
                                 RefRW<LocalTransform>>()
                          .WithAll<IsLocalPlayer>())
        {
            if (!spawnData.ValueRO.hasSpawned && life.ValueRO.isAlive)
            {
                if (SystemAPI.TryGetSingleton<DataContainerComponent>(out var data))
                    spawnData.ValueRW.spawnId = data.selectedSpawnID;

                bool found = false;
                SpawnPointComponent selected = default;

                for (int i = 0; i < spawnPoints.Length; i++)
                {
                    var sp = spawnPoints[i];
                    if (sp.spawnID == spawnData.ValueRO.spawnId &&
                        sp.teamID == (int)team.ValueRO.value &&
                        sp.isActive)
                    {
                        selected = sp;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    for (int i = 0; i < spawnPoints.Length; i++)
                    {
                        var sp = spawnPoints[i];
                        if (sp.teamID == (int)team.ValueRO.value && sp.isActive)
                        {
                            selected = sp;
                            found = true;
                            break;
                        }
                    }
                }

                if (found)
                {
                    spawnData.ValueRW.spawnPosition = selected.position;
                    transform.ValueRW.Position = selected.position;
                    spawnData.ValueRW.hasSpawned = true;
                }
            }
        }

        spawnPoints.Dispose();
    }
}
