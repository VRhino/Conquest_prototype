using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Spawns squad entities and their units when a hero enters the scene.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(HeroSpawnSystem))]
public partial class SquadSpawningSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<SquadSpawnConfigComponent>();
        RequireForUpdate<MatchStateComponent>();
    }

    protected override void OnUpdate()
    {
        Dependency.Complete();
        var spawnConfig = SystemAPI.GetSingleton<SquadSpawnConfigComponent>();
        var dataLookup = GetComponentLookup<SquadDataComponent>(true);
        var defLookup  = GetComponentLookup<SquadDefinitionComponent>(true);
        var isLocalPlayerLookup = GetComponentLookup<IsLocalPlayer>(true);
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (spawn, selection, heroTransform, hero, entity) in SystemAPI
                     .Query<RefRO<HeroSpawnComponent>,
                            RefRO<HeroSquadSelectionComponent>,
                            RefRO<LocalTransform>,
                            RefRO<TeamComponent>>()
                     .WithNone<HeroSquadReference>()
                     .WithEntityAccess())
        {
            if (!spawn.ValueRO.hasSpawned || (SystemAPI.HasComponent<HeroLifeComponent>(entity)
                && !SystemAPI.GetComponent<HeroLifeComponent>(entity).isAlive))
            {
                continue;
            }

            if (!dataLookup.TryGetComponent(selection.ValueRO.squadDataEntity, out var data))
                continue;

            if (!defLookup.TryGetComponent(selection.ValueRO.squadDataEntity, out var def))
                continue;
            if (!def.formationLibrary.IsCreated || def.formationLibrary.Value.formations.Length == 0)
                continue;
            Entity mapOwner = entity;
            if (!SystemAPI.HasBuffer<SquadIdMapElement>(mapOwner) && isLocalPlayerLookup.HasComponent(entity) &&
                SystemAPI.TryGetSingletonEntity<DataContainerComponent>(out var localContainer))
                mapOwner = localContainer;
            SquadIdMapElement snapshot = default;
            if (SystemAPI.HasBuffer<SquadIdMapElement>(mapOwner))
                foreach (var entry in SystemAPI.GetBuffer<SquadIdMapElement>(mapOwner))
                    if (entry.squadId == selection.ValueRO.instanceId) { snapshot = entry; break; }
            int formationIndex = math.clamp(snapshot.formationIndex, 0, def.formationLibrary.Value.formations.Length - 1);
            if (FormationLayout.Capacity(ref def.formationLibrary.Value.formations[formationIndex]) == 0)
                continue;
            bool eliminated = false;
            if (SystemAPI.HasBuffer<InactiveSquadElement>(entity))
                foreach (var reserve in SystemAPI.GetBuffer<InactiveSquadElement>(entity))
                    if (reserve.squadId == selection.ValueRO.instanceId)
                    { eliminated = reserve.isEliminated || reserve.aliveUnits <= 0; break; }
            if (eliminated) continue;
            int initialLevel = snapshot.hasSnapshot ? snapshot.level : 1;
            float initialXP = snapshot.hasSnapshot ? snapshot.currentXP : 0f;
            // Create squad entity (ECS-only, sin visuales)
            Entity squad = ecb.CreateEntity();
#if UNITY_EDITOR
            ecb.SetName(squad, $"Squad_{selection.ValueRO.instanceId}_{def.squadType}");
#endif

            // Configurar posición inicial del squad offset delante del héroe
            quaternion heroRotation = heroTransform.ValueRO.Rotation;
            float3 heroForward = math.forward(heroRotation);
            float3 formationAnchor = heroTransform.ValueRO.Position + heroForward * spawnConfig.squadSpawnOffset;
            ecb.AddComponent(squad, LocalTransform.FromPosition(formationAnchor));

            ecb.AddComponent(squad, new SquadOwnerComponent { hero = entity });
            ecb.AddComponent(squad, new HeroWorldPositionComponent
            {
                position = heroTransform.ValueRO.Position,
                rotation = heroTransform.ValueRO.Rotation
            });
            if (isLocalPlayerLookup.HasComponent(entity))
                ecb.AddComponent<IsLocalSquadActive>(squad);
            // Squad created successfully

            ecb.AddComponent(squad, data); // Add the complete SquadDataComponent
            ecb.AddComponent(squad, new SquadDefinitionComponent
            {
                squadType        = def.squadType,
                behaviorProfile  = def.behaviorProfile,
                formationLibrary = def.formationLibrary,
                unitCount        = def.unitCount,
                GridSize         = def.GridSize,
                leadershipCost   = def.leadershipCost,
                detectionRange   = def.detectionRange
            });
            ecb.AddComponent(squad, new SquadDataReference { dataEntity = squad }); // Self-ref so SquadAISystem can find this squad
            if (SystemAPI.HasBuffer<AbilityByLevelElement>(selection.ValueRO.squadDataEntity))
            {
                var abilitySource = SystemAPI.GetBuffer<AbilityByLevelElement>(selection.ValueRO.squadDataEntity);
                var abilityTarget = ecb.AddBuffer<AbilityByLevelElement>(squad);
                foreach (var ability in abilitySource) abilityTarget.Add(ability);
            }
            // Formation library is carried by SquadDataComponent and SquadDefinitionComponent.
            ecb.AddComponent(squad, new SquadProgressComponent
            {
                level = initialLevel,
                currentXP = initialXP,
                xpToNextLevel = SquadProgressionCurves.XPToNext(
                    data.curves, initialLevel)
            });
            ecb.AddComponent(squad, new SquadInstanceComponent
            {
                id = selection.ValueRO.instanceId, persistentId = snapshot.persistentId
            });

            // Los squads son entidades lógicas sin visual propio
            // Solo las unidades individuales tienen visuales

            // Obtener la primera formación disponible en el arreglo como formación por defecto (índice 0)
            var firstFormationType = def.formationLibrary.Value.formations.Length > 0
                ? def.formationLibrary.Value.formations[formationIndex].formationType
                : FormationType.Line; // Fallback en caso de que no haya formaciones (edge case)

            // Todos los squads comienzan en HoldPosition para que el jugador se oriente antes de mover
            var initialOrder = SquadOrderType.HoldPosition;
            var initialState = SquadFSMState.HoldingPosition;

            // Siempre agregar SquadHoldPositionComponent para que GridFormationUpdateSystem
            // use holdCenter fijo en lugar de seguir al héroe desde el spawn
            ecb.AddComponent(squad, new SquadHoldPositionComponent
            {
                holdCenter = formationAnchor,
                holdRotation = heroRotation,
                originalFormation = firstFormationType
            });

            // Agregar componentes necesarios para el sistema de control y formación
            ecb.AddComponent(squad, new SquadInputComponent
            {
                hasNewOrder = false
            });

            ecb.AddComponent(squad, new SquadStateComponent
            {
                currentOrder = initialOrder,
                isExecutingOrder = false,
                formationChangeCooldown = 0f,
                currentState = initialState,
                transitionTo = initialState,
                stateTimer = 0f,
                lastOwnerAlive = true,
                retreatTriggered = false
            });

            ecb.AddComponent(squad, new FormationComponent
            {
                currentFormation = firstFormationType // Formación inicial: primera del arreglo
            });

            // Buffers de combate y detección de enemigos
            ecb.AddComponent<SquadAIComponent>(squad);
            ecb.AddBuffer<DetectedEnemy>(squad);
            ecb.AddBuffer<SquadTargetEntity>(squad);

            // [Sprint3] Anchor moving tag — starts disabled (hero is stationary at spawn)
            ecb.AddComponent<SquadAnchorMovingTag>(squad);

            // Keep all order views aligned with the initial HoldPosition state.
            ecb.AddComponent(squad, new SquadPlayerOrderIntentComponent
            {
                orderType = initialOrder,
                holdPosition = formationAnchor,
                desiredFormation = firstFormationType
            });
            ecb.AddComponent(squad, new SquadAIOrderIntentComponent
            {
                suggestedOrder = initialOrder,
                holdPosition = formationAnchor
            });
            ecb.AddComponent<SquadCombatReactionIntentComponent>(squad);
            ecb.AddComponent(squad, new SquadResolvedOrderComponent
            {
                order = initialOrder,
                holdPosition = formationAnchor,
                formation = firstFormationType,
                source = isLocalPlayerLookup.HasComponent(entity) ? OrderSource.Player : OrderSource.AI
            });

            // Formation anchor — updated each frame by SquadAnchorSystem
            ecb.AddComponent(squad, new SquadFormationAnchorComponent
            {
                position = formationAnchor,
                rotation = quaternion.identity
            });

            // Create one damage profile entity per squad — all units reference it.
            // Values are taken directly from SquadData; types with value 0 are ignored in calculation.
            Entity squadDamageProfile = ecb.CreateEntity();
            ecb.AddComponent(squad, new SquadDamageProfileReference { Value = squadDamageProfile });
            ecb.AddComponent(squadDamageProfile, new DamageProfileComponent
            {
                bluntDamage         = data.bluntDamage,
                slashingDamage      = data.slashingDamage,
                piercingDamage      = data.piercingDamage,
                bluntPenetration    = data.bluntPenetration,
                slashingPenetration = data.slashingPenetration,
                piercingPenetration = data.piercingPenetration,
            });

            var unitBuffer = ecb.AddBuffer<SquadUnitElement>(squad);

            ref var formations = ref def.formationLibrary.Value.formations;
            ref var firstFormation = ref formations[formationIndex];

            int unitCount = math.min(math.max(0, def.unitCount), FormationLayout.Capacity(ref firstFormation));
            if (snapshot.hasSnapshot) unitCount = math.min(unitCount, snapshot.totalUnits);
            ecb.SetComponent(squad, new SquadInstanceComponent
            {
                id = selection.ValueRO.instanceId, persistentId = snapshot.persistentId,
                initialUnitCount = unitCount
            });

            // Check if this is a re-invocation with reduced units (squad swap)
            int spawnCount = unitCount;
            if (snapshot.hasSnapshot) spawnCount = math.min(spawnCount, snapshot.aliveUnits);
            if (SystemAPI.HasBuffer<InactiveSquadElement>(entity))
            {
                var inactiveBuffer = SystemAPI.GetBuffer<InactiveSquadElement>(entity);
                for (int b = 0; b < inactiveBuffer.Length; b++)
                {
                    if (inactiveBuffer[b].squadId == selection.ValueRO.instanceId)
                    {
                        spawnCount = math.clamp(inactiveBuffer[b].aliveUnits, 0, unitCount);
                        inactiveBuffer.RemoveAt(b);
                        break;
                    }
                }
            }

            // Compute saved-level speed using curve + weight (same formula as UnitStatsUtility)
            // UnitStatScalingSystem will override this on level changes.
            float spawnSpeedMul = 1f;
            if (data.curves.IsCreated)
                spawnSpeedMul = SquadProgressionCurves.Read(ref data.curves.Value.speed, initialLevel);
            int spawnWeightCategory = (int)math.round(data.weight);
            float finalSpeed = UnitSpeedCalculator.CalculateFinalSpeed(data.baseSpeed, spawnSpeedMul, spawnWeightCategory);
            var unitContext = new SquadUnitSpawnContext
            {
                squad = squad,
                hero = entity,
                damageProfile = squadDamageProfile,
                data = data,
                config = spawnConfig,
                squadType = def.squadType,
                instanceId = selection.ValueRO.instanceId,
                speed = finalSpeed,
                rotation = heroRotation
            };

            // Initial layout: template for the soldiers spawned, spacing as authored, seed stable for the battle.
            int templateIndex = FormationLayout.SelectTemplate(ref firstFormation, spawnCount);
            uint layoutSeed = FormationLayout.Seed(selection.ValueRO.instanceId);
            float spawnSpacing = FormationLayout.EffectiveSpacing(firstFormation.spacing, 1f);
            ecb.SetComponent(squad, new FormationComponent
            {
                currentFormation = firstFormationType,
                templateIndex = templateIndex,
                layoutUnitCount = spawnCount,
                spacing = spawnSpacing,
                scale = 1f,
                layoutSeed = layoutSeed
            });
            var spawnState = new SquadStateComponent { currentState = initialState };

            for (int i = 0; i < spawnCount; i++)
            {
                if (!FormationPositionCalculator.TryBuildSlot(ref firstFormation, templateIndex, i, spawnSpacing,
                        layoutSeed, out var slot))
                    break;
                // The anchor already includes squadSpawnOffset.
                float3 worldPos = FormationPositionCalculator.DesiredPosition(slot.localOffset, spawnState, null,
                    formationAnchor, heroRotation, true);
                Entity unit = ecb.CreateEntity();
                SquadUnitSpawnBuilder.Configure(ecb, unit, unitContext, slot, worldPos);
                unitBuffer.Add(new SquadUnitElement { Value = unit });
            }

            // Squad ECS created successfully

            // Crear referencia del squad al héroe
            ecb.AddComponent(entity, new HeroSquadReference { squad = squad });

            // Copiar el team del héroe al squad y sus unidades
            if (SystemAPI.HasComponent<TeamComponent>(entity))
            {
                var heroTeam = SystemAPI.GetComponent<TeamComponent>(entity);

                ecb.AddComponent(squad, heroTeam);

                // Aplicar el team a todas las unidades del squad
                for (int i = 0; i < unitBuffer.Length; i++)
                {
                    Entity unit = unitBuffer[i].Value;
                    ecb.AddComponent(unit, heroTeam);
                }
            }
            else
            {
                Debug.LogWarning($"Squad spawn: hero {entity} NO tiene TeamComponent al spawnear");
            }

            // Initialize InactiveSquadElement buffer for squad swap (only on first spawn, when buffer doesn't exist yet)
            if (!SystemAPI.HasBuffer<InactiveSquadElement>(entity))
            {
                var inactiveBuffer = ecb.AddBuffer<InactiveSquadElement>(entity);
                if (SystemAPI.HasBuffer<SquadIdMapElement>(mapOwner))
                {
                    // Resolve definition sizes once instead of scanning every definition
                    // for every reserve entry.
                    var unitCountsById = new Dictionary<FixedString64Bytes, int>();
                    foreach (var (idComp, defComp) in SystemAPI
                                 .Query<RefRO<SquadDataIDComponent>, RefRO<SquadDefinitionComponent>>())
                    {
                        var id = idComp.ValueRO.id;
                        if (!unitCountsById.ContainsKey(id))
                            unitCountsById.Add(id, math.max(0, defComp.ValueRO.unitCount));
                    }

                    var mapBuffer = SystemAPI.GetBuffer<SquadIdMapElement>(mapOwner);
                    for (int m = 0; m < mapBuffer.Length; m++)
                    {
                        var map = mapBuffer[m];
                        if (map.squadId == selection.ValueRO.instanceId)
                            continue;

                        int totalUnitsForSquad = unitCountsById.TryGetValue(map.baseSquadID, out int count)
                            ? count : unitCount;
                        if (map.hasSnapshot)
                            totalUnitsForSquad = math.min(totalUnitsForSquad, map.totalUnits);
                        int aliveUnitsForSquad = map.hasSnapshot
                            ? math.min(totalUnitsForSquad, map.aliveUnits) : totalUnitsForSquad;
                        inactiveBuffer.Add(new InactiveSquadElement
                        {
                            squadId = map.squadId,
                            baseSquadID = map.baseSquadID,
                            aliveUnits = aliveUnitsForSquad,
                            totalUnits = totalUnitsForSquad,
                            isEliminated = aliveUnitsForSquad == 0
                        });
                    }
                }
            }
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
