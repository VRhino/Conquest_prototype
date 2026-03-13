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
public partial class SquadSwapExecutionSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (executeTag, team, entity) in SystemAPI
                     .Query<RefRO<SquadSwapExecuteTag>,
                            RefRO<TeamComponent>>()
                     .WithEntityAccess())
        {
            int newSquadId = executeTag.ValueRO.newSquadId;

            // Get current squad via HeroSquadReference
            if (!SystemAPI.HasComponent<HeroSquadReference>(entity))
            {
                ecb.RemoveComponent<SquadSwapExecuteTag>(entity);
                continue;
            }

            Entity oldSquad = SystemAPI.GetComponent<HeroSquadReference>(entity).squad;

            // Read old squad's instance ID for persistence
            int oldSquadId = 0;
            if (SystemAPI.HasComponent<SquadInstanceComponent>(oldSquad))
            {
                oldSquadId = SystemAPI.GetComponent<SquadInstanceComponent>(oldSquad).id;
            }

            // --- Retire old squad ---
            if (SystemAPI.HasComponent<SquadStateComponent>(oldSquad))
            {
                var oldState = SystemAPI.GetComponent<SquadStateComponent>(oldSquad);
                oldState.transitionTo = SquadFSMState.Retreating;
                oldState.retreatTriggered = true;
                SystemAPI.SetComponent(oldSquad, oldState);
            }

            // Find spawn point for retreat target
            float3 retreatTarget = float3.zero;
            int heroTeamId = (int)team.ValueRO.value;
            foreach (var spawnPoint in SystemAPI.Query<RefRO<SpawnPointComponent>>())
            {
                if (spawnPoint.ValueRO.teamID == heroTeamId && spawnPoint.ValueRO.isActive)
                {
                    retreatTarget = spawnPoint.ValueRO.position;
                    break;
                }
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

            // Remove IsLocalPlayer from old squad so singleton queries
            // (e.g. SquadSectionController) don't find two matches.
            ecb.RemoveComponent<IsLocalPlayer>(oldSquad);

            // --- Prepare new squad ---
            // Find the baseSquadID for the new squad from InactiveSquadElement buffer
            FixedString64Bytes newBaseSquadID = default;
            if (SystemAPI.HasBuffer<InactiveSquadElement>(entity))
            {
                var inactiveBuffer = SystemAPI.GetBuffer<InactiveSquadElement>(entity);
                for (int i = 0; i < inactiveBuffer.Length; i++)
                {
                    if (inactiveBuffer[i].squadId == newSquadId)
                    {
                        newBaseSquadID = inactiveBuffer[i].baseSquadID;
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

            if (newSquadDataEntity == Entity.Null)
            {
                UnityEngine.Debug.LogWarning($"[SquadSwapExecutionSystem] Could not find SquadDataIDComponent for baseSquadID '{newBaseSquadID}'");
                ecb.RemoveComponent<SquadSwapExecuteTag>(entity);
                continue;
            }

            // Add old squad to InactiveSquadElement buffer before removing reference
            if (SystemAPI.HasBuffer<InactiveSquadElement>(entity))
            {
                var inactiveBuffer = SystemAPI.GetBuffer<InactiveSquadElement>(entity);

                // Get old squad's baseSquadID from SquadDataIDComponent lookup
                FixedString64Bytes oldBaseSquadID = default;
                if (SystemAPI.HasComponent<SquadDataComponent>(oldSquad))
                {
                    // Find matching SquadDataIDComponent by checking all entries
                    var dcQuery = GetEntityQuery(ComponentType.ReadOnly<DataContainerComponent>());
                    if (!dcQuery.IsEmptyIgnoreFilter)
                    {
                        Entity dcEntity = dcQuery.GetSingletonEntity();
                        if (SystemAPI.HasBuffer<SquadIdMapElement>(dcEntity))
                        {
                            var mapBuffer = SystemAPI.GetBuffer<SquadIdMapElement>(dcEntity);
                            for (int m = 0; m < mapBuffer.Length; m++)
                            {
                                if (mapBuffer[m].squadId == oldSquadId)
                                {
                                    oldBaseSquadID = mapBuffer[m].baseSquadID;
                                    break;
                                }
                            }
                        }
                    }
                }

                // Count alive units in old squad
                int aliveCount = 0;
                int totalCount = 0;
                if (SystemAPI.HasBuffer<SquadUnitElement>(oldSquad))
                {
                    var unitBuffer = SystemAPI.GetBuffer<SquadUnitElement>(oldSquad);
                    totalCount = unitBuffer.Length;
                    for (int u = 0; u < unitBuffer.Length; u++)
                    {
                        Entity unitEntity = unitBuffer[u].Value;
                        if (SystemAPI.Exists(unitEntity) && !SystemAPI.HasComponent<IsDeadComponent>(unitEntity))
                        {
                            aliveCount++;
                        }
                    }
                }

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
            Entity evt = ecb.CreateEntity();
            ecb.AddComponent(evt, new SquadChangeEvent { newSquadId = newSquadId });

            // Clean up
            ecb.RemoveComponent<SquadSwapExecuteTag>(entity);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
