using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Executes the actual squad swap when <see cref="SquadSwapExecuteTag"/> is present.
/// Retires the current squad and prepares the hero for a new squad spawn.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(SquadSwapChannelingSystem))]
[UpdateBefore(typeof(SquadSpawningSystem))]
[UpdateBefore(typeof(SquadOrderSystem))]
public partial class SquadSwapExecutionSystem : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<MatchStateComponent>();
    }

    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (executeTag, team, entity) in SystemAPI
                     .Query<RefRO<SquadSwapExecuteTag>,
                            RefRO<TeamComponent>>()
                     .WithEntityAccess())
        {
            int newSquadId = executeTag.ValueRO.newSquadId;
            bool stillRetreating = false;
            foreach (var retreating in SystemAPI.Query<RefRO<SquadRetreatingFromSwapTag>>())
                if (retreating.ValueRO.heroEntity == entity && retreating.ValueRO.squadId == newSquadId)
                    stillRetreating = true;
            if (stillRetreating)
            {
                ecb.RemoveComponent<SquadSwapExecuteTag>(entity);
                continue;
            }

            // Get current squad via HeroSquadReference
            if (!SystemAPI.HasComponent<HeroSquadReference>(entity))
            {
                ecb.RemoveComponent<SquadSwapExecuteTag>(entity);
                continue;
            }

            Entity oldSquad = SystemAPI.GetComponent<HeroSquadReference>(entity).squad;
            if (!SystemAPI.Exists(oldSquad)
                || !SystemAPI.HasComponent<SquadInstanceComponent>(oldSquad)
                || !SystemAPI.HasComponent<SquadStateComponent>(oldSquad)
                || !SystemAPI.HasBuffer<SquadUnitElement>(oldSquad)
                || !SystemAPI.HasComponent<HeroSquadSelectionComponent>(entity)
                || !SystemAPI.HasComponent<HeroLifeComponent>(entity)
                || !SystemAPI.GetComponent<HeroLifeComponent>(entity).isAlive
                || SystemAPI.HasComponent<RetreatComponent>(oldSquad))
            {
                ecb.RemoveComponent<SquadSwapExecuteTag>(entity);
                continue;
            }

            // Read old squad's instance ID for persistence
            int oldSquadId = 0;
            if (SystemAPI.HasComponent<SquadInstanceComponent>(oldSquad))
            {
                oldSquadId = SystemAPI.GetComponent<SquadInstanceComponent>(oldSquad).id;
            }

            // --- Prepare new squad ---
            // Find the baseSquadID for the new squad from InactiveSquadElement buffer
            FixedString64Bytes newBaseSquadID = default;
            bool reserveAvailable = false;
            if (SystemAPI.HasBuffer<InactiveSquadElement>(entity))
            {
                var inactiveBuffer = SystemAPI.GetBuffer<InactiveSquadElement>(entity);
                for (int i = 0; i < inactiveBuffer.Length; i++)
                {
                    if (inactiveBuffer[i].squadId == newSquadId)
                    {
                        newBaseSquadID = inactiveBuffer[i].baseSquadID;
                        reserveAvailable = !inactiveBuffer[i].isEliminated && inactiveBuffer[i].aliveUnits > 0;
                        break;
                    }
                }
            }

            // Find the SquadDataIDComponent entity matching the baseSquadID
            Entity newSquadDataEntity = Entity.Null;
            foreach (var (idComp, e) in SystemAPI
                         .Query<RefRO<SquadDataIDComponent>>()
                         .WithEntityAccess())
            {
                if (idComp.ValueRO.id == newBaseSquadID)
                {
                    newSquadDataEntity = e;
                    break;
                }
            }

            if (!reserveAvailable || newSquadId == oldSquadId || newSquadDataEntity == Entity.Null
                || !SystemAPI.HasComponent<SquadDataComponent>(newSquadDataEntity)
                || !SystemAPI.HasComponent<SquadDefinitionComponent>(newSquadDataEntity)
                || !SystemAPI.HasComponent<HeroSpawnComponent>(entity)
                || !SystemAPI.GetComponent<HeroSpawnComponent>(entity).hasSpawned
                || !SystemAPI.HasComponent<LocalTransform>(entity)
                || !SystemAPI.HasSingleton<SquadSpawnConfigComponent>())
            {
                ecb.RemoveComponent<SquadSwapExecuteTag>(entity);
                continue;
            }

            var definition = SystemAPI.GetComponent<SquadDefinitionComponent>(newSquadDataEntity);
            if (!definition.formationLibrary.IsCreated || definition.formationLibrary.Value.formations.Length == 0
                || definition.unitCount <= 0 || !SystemAPI.Exists(definition.unitPrefab))
            {
                ecb.RemoveComponent<SquadSwapExecuteTag>(entity);
                continue;
            }
            Entity mapOwner = entity;
            if (!SystemAPI.HasBuffer<SquadIdMapElement>(mapOwner) && SystemAPI.HasComponent<IsLocalPlayer>(entity)
                && SystemAPI.TryGetSingletonEntity<DataContainerComponent>(out var localContainer))
                mapOwner = localContainer;
            int formationIndex = 0;
            bool snapshotAllowsSpawn = true;
            if (SystemAPI.HasBuffer<SquadIdMapElement>(mapOwner))
                foreach (var entry in SystemAPI.GetBuffer<SquadIdMapElement>(mapOwner))
                    if (entry.squadId == newSquadId)
                    {
                        formationIndex = math.clamp(entry.formationIndex, 0, definition.formationLibrary.Value.formations.Length - 1);
                        snapshotAllowsSpawn = !entry.hasSnapshot || entry.totalUnits > 0;
                        break;
                    }
            if (!snapshotAllowsSpawn || definition.formationLibrary.Value.formations[formationIndex].gridPositions.Length == 0)
            {
                ecb.RemoveComponent<SquadSwapExecuteTag>(entity);
                continue;
            }

            // Find spawn point for retreat target
            float3 retreatTarget = float3.zero;
            bool hasRetreatTarget = false;
            int heroTeamId = (int)team.ValueRO.value;
            foreach (var spawnPoint in SystemAPI.Query<RefRO<SpawnPointComponent>>())
            {
                if (spawnPoint.ValueRO.teamID == heroTeamId && spawnPoint.ValueRO.isActive)
                {
                    retreatTarget = spawnPoint.ValueRO.position;
                    hasRetreatTarget = true;
                    break;
                }
            }

            if (!hasRetreatTarget)
            {
                ecb.RemoveComponent<SquadSwapExecuteTag>(entity);
                continue;
            }
            // All replacement prerequisites are valid before any retirement mutation.
            if (SystemAPI.HasComponent<SquadStateComponent>(oldSquad))
            {
                var oldState = SystemAPI.GetComponent<SquadStateComponent>(oldSquad);
                oldState.currentState = oldState.transitionTo = SquadFSMState.Retreating;
                oldState.retreatTriggered = true;
                ecb.SetComponent(oldSquad, oldState);
            }

            ecb.AddComponent(oldSquad, new RetreatComponent
            {
                retreatTarget = retreatTarget,
                retreatTimer = 0f,
                retreatDuration = 5f
            });

            // Add SquadNavigationComponent if not present
            if (!SystemAPI.HasComponent<SquadNavigationComponent>(oldSquad))
            {
                ecb.AddComponent(oldSquad, new SquadNavigationComponent
                {
                    targetPosition = retreatTarget,
                    isNavigating = true,
                    arrivalThreshold = 0.5f
                });
            }

            // Tag for persistence during retreat cleanup
            ecb.AddComponent(oldSquad, new SquadRetreatingFromSwapTag
            {
                squadId = oldSquadId,
                heroEntity = entity
            });

            // Remove the active-squad marker so singleton queries
            // (e.g. SquadSectionController) don't find two matches.
            ecb.RemoveComponent<IsLocalSquadActive>(oldSquad);

            // Add old squad to InactiveSquadElement buffer before removing reference
            if (SystemAPI.HasBuffer<InactiveSquadElement>(entity))
            {
                var inactiveBuffer = SystemAPI.GetBuffer<InactiveSquadElement>(entity);

                // Get old squad's baseSquadID from SquadDataIDComponent lookup
                FixedString64Bytes oldBaseSquadID = default;
                if (SystemAPI.HasBuffer<SquadIdMapElement>(mapOwner))
                    foreach (var entry in SystemAPI.GetBuffer<SquadIdMapElement>(mapOwner))
                        if (entry.squadId == oldSquadId) { oldBaseSquadID = entry.baseSquadID; break; }
                var oldDefinition = SystemAPI.GetComponent<HeroSquadSelectionComponent>(entity).squadDataEntity;
                if (oldBaseSquadID.IsEmpty && SystemAPI.HasComponent<SquadDataIDComponent>(oldDefinition))
                    oldBaseSquadID = SystemAPI.GetComponent<SquadDataIDComponent>(oldDefinition).id;

                // Count alive units in old squad
                int aliveCount = 0;
                int totalCount = 0;
                if (SystemAPI.HasBuffer<SquadUnitElement>(oldSquad))
                {
                    var unitBuffer = SystemAPI.GetBuffer<SquadUnitElement>(oldSquad);
                    totalCount = math.max(unitBuffer.Length, SystemAPI.GetComponent<SquadInstanceComponent>(oldSquad).initialUnitCount);
                    for (int u = 0; u < unitBuffer.Length; u++)
                    {
                        Entity unitEntity = unitBuffer[u].Value;
                        if (SystemAPI.Exists(unitEntity) && !SystemAPI.HasComponent<IsDeadComponent>(unitEntity))
                        {
                            aliveCount++;
                        }
                    }
                }

                for (int i = inactiveBuffer.Length - 1; i >= 0; i--)
                    if (inactiveBuffer[i].squadId == oldSquadId) inactiveBuffer.RemoveAt(i);
                inactiveBuffer.Add(new InactiveSquadElement
                {
                    squadId = oldSquadId,
                    baseSquadID = oldBaseSquadID,
                    aliveUnits = aliveCount,
                    totalUnits = totalCount,
                    isEliminated = aliveCount == 0
                });
            }

            // Update HeroSquadSelectionComponent for the new squad
            ecb.SetComponent(entity, new HeroSquadSelectionComponent
            {
                squadDataEntity = newSquadDataEntity,
                instanceId = newSquadId
            });

            // Remove HeroSquadReference to trigger SquadSpawningSystem on next frame
            ecb.RemoveComponent<HeroSquadReference>(entity);

            // Emit SquadChangeEvent
            ecb.AddComponent(entity, new SquadSwapCooldownComponent { remainingTime = 10f });
            Entity evt = ecb.CreateEntity();
            ecb.AddComponent(evt, new SquadChangeEvent { newSquadId = newSquadId });

            // Clean up
            ecb.RemoveComponent<SquadSwapExecuteTag>(entity);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
