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
            Debug.LogWarning($"[HeroSpawnSystem] No exact match for spawnID={dataForInstantiate.selectedSpawnID}+teamID={dataForInstantiate.teamID}. Trying fallback...");
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

        if (!found)
        {
            Debug.LogError($"[HeroSpawnSystem] NO spawn point found for teamID={dataForInstantiate.teamID}. Hero will NOT spawn.");
        }

        if (found)
        {
            // Calcular la altura correcta del terreno
            var spawnPosition = selected.position;
            spawnPosition.y = FormationPositionCalculator.calculateTerraindHeight(spawnPosition);

            // Instanciación híbrida: crear solo la entidad ECS (sin visual)
            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var heroEntity = entityManager.Instantiate(heroPrefab.prefab);
#if UNITY_EDITOR
            entityManager.SetName(heroEntity, $"Hero_Team{(int)dataForInstantiate.teamID}");
#endif
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

            // Ensure HeroHealthComponent is initialized so DamageCalculationSystem can apply damage.
            // Read real max HP from cache (populated by PlayerSessionService.SetSelectedHero before battle).
            float realMaxHealth = 0f;
            if (PlayerSessionService.SelectedHero != null)
            {
                var selectedHero = PlayerSessionService.SelectedHero;
                string cacheKey = string.IsNullOrEmpty(selectedHero.heroName) ? selectedHero.classId : selectedHero.heroName;
                realMaxHealth = DataCacheService.GetHeroCalculatedAttributes(cacheKey).maxHealth;

            }
            if (realMaxHealth <= 0f) realMaxHealth = 200f; // fallback de emergencia

            if (entityManager.HasComponent<HeroHealthComponent>(heroEntity))
            {
                var heroHealth = entityManager.GetComponentData<HeroHealthComponent>(heroEntity);
                heroHealth.maxHealth = realMaxHealth;
                heroHealth.currentHealth = realMaxHealth;
                entityManager.SetComponentData(heroEntity, heroHealth);
            }
            else
            {
                entityManager.AddComponentData(heroEntity, new HeroHealthComponent
                {
                    maxHealth = realMaxHealth,
                    currentHealth = realMaxHealth
                });
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
            if (_enableDebugLogs)
                Debug.Log($"[HeroSpawnSystem] selectedSquadBaseID='{dataForInstantiate.selectedSquadBaseID}'");
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
                    Debug.LogWarning($"[HeroSpawnSystem] Squad '{dataForInstantiate.selectedSquadBaseID}' NOT found in ECS.");
                }
            }
            else
            {
                Debug.LogError("[HeroSpawnSystem] selectedSquadBaseID is EMPTY — HeroSquadSelectionComponent will NOT be added, squads will not spawn.");
            }


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
