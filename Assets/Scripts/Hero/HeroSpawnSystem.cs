using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Places the local hero at the selected spawn point at the start of the match
/// and after a respawn.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class HeroSpawnSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Instanciar el héroe local si no existe
        bool heroExists = false;
        foreach (var _ in SystemAPI.Query<RefRO<IsLocalPlayer>>())
        {
            heroExists = true;
            break;
        }
        
        if (!heroExists && SystemAPI.TryGetSingleton<HeroPrefabComponent>(out var heroPrefab) && SystemAPI.TryGetSingleton<DataContainerComponent>(out var dataForInstantiate))
        {
            var spawnPointQueryForInstantiate = GetEntityQuery(ComponentType.ReadOnly<SpawnPointComponent>());
            var spawnPointsForInstantiate = spawnPointQueryForInstantiate.ToComponentDataArray<SpawnPointComponent>(Allocator.Temp);
            SpawnPointComponent selected = default;
            bool found = false;
            for (int i = 0; i < spawnPointsForInstantiate.Length; i++)
            {
                var sp = spawnPointsForInstantiate[i];
                if (sp.spawnID == dataForInstantiate.selectedSpawnID && sp.teamID == dataForInstantiate.teamID && sp.isActive)
                {
                    selected = sp;
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                for (int i = 0; i < spawnPointsForInstantiate.Length; i++)
                {
                    var sp = spawnPointsForInstantiate[i];
                    if (sp.teamID == dataForInstantiate.teamID && sp.isActive)
                    {
                        selected = sp;
                        found = true;
                        break;
                    }
                }
            }
            if (found)
            {
                // Instanciación híbrida: crear solo la entidad ECS (sin visual)
                var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
                var heroEntity = entityManager.Instantiate(heroPrefab.prefab);
                entityManager.SetComponentData(heroEntity, new LocalTransform { 
                    Position = selected.position, 
                    Rotation = Unity.Mathematics.quaternion.identity, 
                    Scale = 1f 
                });
                
                // El HeroVisualManagementSystem se encargará de crear el GameObject visual
                Debug.Log($"[HeroSpawnSystem] Entidad del héroe instanciada en {selected.position}. " +
                          $"El visual será creado por HeroVisualManagementSystem.");
            }
            spawnPointsForInstantiate.Dispose();
        }

        var spawnPointQuery = GetEntityQuery(ComponentType.ReadOnly<SpawnPointComponent>());
        var spawnPoints = spawnPointQuery.ToComponentDataArray<SpawnPointComponent>(Allocator.Temp);
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
