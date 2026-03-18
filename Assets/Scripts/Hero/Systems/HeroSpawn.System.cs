using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Places the local hero at the selected spawn point at the start of the match
/// and after a respawn.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class HeroSpawnSystem : SystemBase
{
    private bool _enableDebugLogs = false;

    protected override void OnUpdate()
    {
        // Solo verificar spawning cada pocos frames para evitar condiciones de carrera
        if (SystemAPI.Time.ElapsedTime < 1.0f || UnityEngine.Time.frameCount % 30 != 0)
        {
            // Continuar con la lógica de posicionamiento para héroes ya existentes
            HandleExistingHeroPositioning();
            return;
        }

        // Instanciar el héroe local si no existe
        bool heroExists = false;

        // Verificar si ya existe una entidad héroe con IsLocalPlayer
        var heroQuery = GetEntityQuery(ComponentType.ReadOnly<IsLocalPlayer>());
        heroExists = !heroQuery.IsEmpty;

        if (_enableDebugLogs)
        {
            Debug.Log($"[HeroSpawnSystem] Hero exists check: {heroExists}, Hero count: {heroQuery.CalculateEntityCount()}");
        }

        if (!heroExists && SystemAPI.TryGetSingleton<HeroPrefabComponent>(out var heroPrefab) && SystemAPI.TryGetSingleton<DataContainerComponent>(out var dataForInstantiate))
        {
            SpawnNewHero(heroPrefab, dataForInstantiate);
        }

        // Manejar posicionamiento de héroes existentes
        HandleExistingHeroPositioning();
    }

    private void SpawnNewHero(HeroPrefabComponent heroPrefab, DataContainerComponent dataForInstantiate)
    {
        if (_enableDebugLogs)
        {
            Debug.Log("[HeroSpawnSystem] Attempting to spawn hero - no existing hero found");
        }

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
            // Calcular la altura correcta del terreno
            var spawnPosition = selected.position;
            spawnPosition.y = FormationPositionCalculator.calculateTerraindHeight(spawnPosition);

            // Instanciación híbrida: crear solo la entidad ECS (sin visual)
            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var heroEntity = entityManager.Instantiate(heroPrefab.prefab);
            var spawnRotation = CalculateSpawnRotation(spawnPosition, (int)dataForInstantiate.teamID);
            entityManager.SetComponentData(heroEntity, new LocalTransform
            {
                Position = spawnPosition,
                Rotation = spawnRotation,
                Scale = 1f
            });
            Debug.Log($"[HeroSpawnSystem.cs][{heroEntity}] Set LocalTransform.Position = {spawnPosition}");

            // Garantizar isAlive = true (bool defaultea false en prefab bakeado)
            if (entityManager.HasComponent<HeroLifeComponent>(heroEntity))
            {
                var life = entityManager.GetComponentData<HeroLifeComponent>(heroEntity);
                life.isAlive = true;
                entityManager.SetComponentData(heroEntity, life);
            }
            else
            {
                entityManager.AddComponentData(heroEntity, new HeroLifeComponent { isAlive = true });
            }

            // Marcar como spawneado para evitar re-spawning
            if (entityManager.HasComponent<HeroSpawnComponent>(heroEntity))
            {
                var spawnComponent = entityManager.GetComponentData<HeroSpawnComponent>(heroEntity);
                spawnComponent.hasSpawned = true;
                spawnComponent.spawnPosition = spawnPosition;
                spawnComponent.spawnRotation = spawnRotation;
                spawnComponent.spawnId = dataForInstantiate.selectedSpawnID;
                entityManager.SetComponentData(heroEntity, spawnComponent);
            }

            // Asignar squad al hero si BattleData proveyó un squad ID
            if (!dataForInstantiate.selectedSquadBaseID.IsEmpty)
            {
                Entity squadDataEntity = Entity.Null;
                foreach (var (idComp, e) in SystemAPI
                    .Query<RefRO<SquadDataIDComponent>>()
                    .WithEntityAccess())
                {
                    if (idComp.ValueRO.id == dataForInstantiate.selectedSquadBaseID)
                    {
                        squadDataEntity = e;
                        break;
                    }
                }

                if (squadDataEntity != Entity.Null)
                {
                    entityManager.AddComponentData(heroEntity, new HeroSquadSelectionComponent
                    {
                        squadDataEntity = squadDataEntity,
                        instanceId = 0
                    });
                    Debug.Log($"[HeroSpawnSystem] Squad '{dataForInstantiate.selectedSquadBaseID}' asignado al hero entity {heroEntity}");
                }
                else
                {
                    Debug.LogWarning($"[HeroSpawnSystem] No se encontró SquadDataIDComponent con id='{dataForInstantiate.selectedSquadBaseID}'");
                }
            }

            // Añadir HeroStateComponent para que CameraBootstrapSystem pueda encontrar al héroe
            // (SquadSpawningSystem también lo añade, pero no corre en FeudoScene sin squads)
            entityManager.AddComponentData(heroEntity, new HeroStateComponent { State = HeroState.Idle });

            if (_enableDebugLogs)
            {
                Debug.Log($"[HeroSpawnSystem] Hero spawned successfully at position: {spawnPosition}");
            }
        }

        spawnPointsForInstantiate.Dispose();
    }

    private void HandleExistingHeroPositioning()
    {
        var spawnPointQuery = GetEntityQuery(ComponentType.ReadOnly<SpawnPointComponent>());
        var spawnPoints = spawnPointQuery.ToComponentDataArray<SpawnPointComponent>(Allocator.Temp);

        foreach (var (life, spawnData, team, transform) in SystemAPI.Query<RefRO<HeroLifeComponent>,
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
                    var correctedPosition = selected.position;
                    correctedPosition.y = FormationPositionCalculator.calculateTerraindHeight(correctedPosition);

                    var rotation = CalculateSpawnRotation(correctedPosition, (int)team.ValueRO.value);
                    spawnData.ValueRW.spawnPosition = correctedPosition;
                    spawnData.ValueRW.spawnRotation = rotation;
                    transform.ValueRW.Position = correctedPosition;
                    transform.ValueRW.Rotation = rotation;
                    Debug.Log($"[HeroSpawnSystem.cs] Set LocalTransform.Position = {correctedPosition}");
                    spawnData.ValueRW.hasSpawned = true;

                    if (_enableDebugLogs)
                    {
                        Debug.Log($"[HeroSpawnSystem] Hero position updated to terrain height: {correctedPosition}");
                    }
                }
            }
        }

        spawnPoints.Dispose();
    }

    /// <summary>
    /// Calculates a rotation facing the enemy's final capture zone so the hero
    /// spawns looking toward the objective.
    /// </summary>
    private quaternion CalculateSpawnRotation(float3 spawnPos, int heroTeamId)
    {
        var zoneQuery = GetEntityQuery(ComponentType.ReadOnly<ZoneTriggerComponent>(), ComponentType.ReadOnly<LocalTransform>());
        var zoneEntities = zoneQuery.ToEntityArray(Allocator.Temp);

        quaternion result = quaternion.identity;

        for (int i = 0; i < zoneEntities.Length; i++)
        {
            var zone = EntityManager.GetComponentData<ZoneTriggerComponent>(zoneEntities[i]);
            if (zone.isFinal && zone.teamOwner != heroTeamId)
            {
                var zoneTransform = EntityManager.GetComponentData<LocalTransform>(zoneEntities[i]);
                float3 direction = zoneTransform.Position - spawnPos;
                direction.y = 0f; // XZ plane only
                if (math.lengthsq(direction) > 0.001f)
                {
                    result = quaternion.LookRotationSafe(math.normalize(direction), math.up());
                }
                break;
            }
        }

        zoneEntities.Dispose();
        return result;
    }
}
