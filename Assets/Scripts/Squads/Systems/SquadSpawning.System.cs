using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

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
            if (!spawn.ValueRO.hasSpawned)
            {
                continue;
            }

            if (!dataLookup.TryGetComponent(selection.ValueRO.squadDataEntity, out var data))
                continue;

            if (!defLookup.TryGetComponent(selection.ValueRO.squadDataEntity, out var def))
                continue;
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
                unitPrefab       = def.unitPrefab,
                leadershipCost   = def.leadershipCost,
                detectionRange   = def.detectionRange
            });
            ecb.AddComponent(squad, new SquadDataReference { dataEntity = squad }); // Self-ref so SquadAISystem can find this squad
            ecb.AddComponent(squad, new SquadStatsComponent
            {
                squadType = def.squadType,
                behaviorProfile = def.behaviorProfile
            });
            // Note: Formation library is now included directly in SquadDataComponent
            ecb.AddComponent(squad, new SquadProgressComponent
            {
                level = 1,
                currentXP = 0f,
                xpToNextLevel = 100f
            });
            ecb.AddComponent(squad, new SquadInstanceComponent { id = selection.ValueRO.instanceId });

            // Los squads son entidades lógicas sin visual propio
            // Solo las unidades individuales tienen visuales

            // Obtener la primera formación disponible en el arreglo como formación por defecto (índice 0)
            var firstFormationType = def.formationLibrary.Value.formations.Length > 0
                ? def.formationLibrary.Value.formations[0].formationType
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
                orderType = initialOrder,
                hasNewOrder = false,
                desiredFormation = firstFormationType, // Formación inicial: primera del arreglo
                holdPosition = formationAnchor // Posición de hold = spawn del squad
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

            // Agregar buffer para el patrón de formación
            ecb.AddBuffer<FormationPatternElement>(squad);

            // [Sprint2] Nuevos componentes — fuentes de verdad futuras (dual-write activo)
            ecb.AddComponent(squad, new SquadCombatStateComponent
            {
                isInCombat    = false,
                engagedTarget = Entity.Null
            });
            ecb.AddComponent(squad, new SquadFSMComponent
            {
                currentState = initialState,
                stateTimer   = 0f
            });
            ecb.AddComponent(squad, new SquadActiveFormationComponent
            {
                currentFormation        = firstFormationType,
                formationChangeCooldown = 0f
            });

            // Buffers de combate y detección de enemigos
            ecb.AddComponent<SquadAIComponent>(squad);
            ecb.AddBuffer<DetectedEnemy>(squad);
            ecb.AddBuffer<SquadTargetEntity>(squad);

            // [Sprint3] Anchor moving tag — starts disabled (hero is stationary at spawn)
            ecb.AddComponent<SquadAnchorMovingTag>(squad);

            // [Sprint6] Intent/Resolution components
            ecb.AddComponent<SquadPlayerOrderIntentComponent>(squad);
            ecb.AddComponent<SquadAIOrderIntentComponent>(squad);
            ecb.AddComponent<SquadCombatReactionIntentComponent>(squad);
            ecb.AddComponent<SquadResolvedOrderComponent>(squad);

            // Formation anchor — updated each frame by SquadAnchorSystem
            ecb.AddComponent(squad, new SquadFormationAnchorComponent
            {
                position = formationAnchor,
                rotation = quaternion.identity
            });

            // Create one damage profile entity per squad — all units reference it.
            // Values are taken directly from SquadData; types with value 0 are ignored in calculation.
            Entity squadDamageProfile = ecb.CreateEntity();
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
            ref var firstFormation = ref formations[0]; // Always use index 0 for initial spawn

            int unitCount = firstFormation.gridPositions.Length;

            // Check if this is a re-invocation with reduced units (squad swap)
            int spawnCount = unitCount;
            if (SystemAPI.HasBuffer<InactiveSquadElement>(entity))
            {
                var inactiveBuffer = SystemAPI.GetBuffer<InactiveSquadElement>(entity);
                for (int b = 0; b < inactiveBuffer.Length; b++)
                {
                    if (inactiveBuffer[b].squadId == selection.ValueRO.instanceId)
                    {
                        spawnCount = inactiveBuffer[b].aliveUnits;
                        inactiveBuffer.RemoveAt(b);
                        break;
                    }
                }
            }

            // Compute level-1 speed using curve + weight (same formula as UnitStatsUtility)
            // UnitStatScalingSystem will override this on level changes.
            float spawnSpeedMul = data.curves.IsCreated ? data.curves.Value.speed[0] : 1f; // index 0 = level 1
            int spawnWeightCategory = (int)math.round(data.weight);
            float finalSpeed = UnitSpeedCalculator.CalculateFinalSpeed(data.baseSpeed, spawnSpeedMul, spawnWeightCategory);

            for (int i = 0; i < spawnCount; i++)
            {
                // Crear unidad ECS (solo lógica, sin visuales)
                Entity unit = ecb.CreateEntity();
#if UNITY_EDITOR
                ecb.SetName(unit, $"Unit_{i}_{def.squadType}_Sq{selection.ValueRO.instanceId}");
#endif
                FormationPositionCalculator.CalculateDesiredPosition(
                    unit,
                    ref firstFormation.gridPositions,
                    i, // unitIndex
                    new SquadStateComponent { currentState = initialState },
                    null,
                    formationAnchor, // usar el ancla del squad que ya incluye squadSpawnOffset
                    out int2 originalGridPos,
                    out float3 gridOffset,
                    out float3 worldPos,
                    true,
                    heroRotation);

                ecb.AddComponent(unit, new LocalTransform
                {
                    Position = worldPos,
                    Rotation = heroRotation,
                    Scale = 1f
                });

                // Agregar referencia visual para la unidad
                ecb.AddComponent(unit, new UnitVisualReference
                {
                    visualPrefabName = GetUnitVisualPrefabName(def.squadType)
                });

                // Añadir componentes ECS de la unidad (todos excepto visuales)
                ecb.AddComponent(unit, new UnitEquipmentComponent
                {
                    armorPercent = 100f,
                    hasDebuff = false,
                    isDeployable = true
                });

                ecb.AddComponent<UnitCombatComponent>(unit);
                ecb.AddComponent(unit, new UnitSpacingComponent
                {
                    minDistance = spawnConfig.unitMinDistance,
                    repelForce = spawnConfig.unitRepelForce,
                    Slot = originalGridPos // Usar posición original para Slot
                });
                ecb.AddComponent(unit, new UnitTargetPositionComponent { position = worldPos });
                ecb.AddComponent(unit, new UnitFormationStateComponent { State = UnitFormationState.Formed }); // Units spawn already at their formation slot, no need to seek it

                // Formation lifecycle milestone tags (pulse signals, start disabled)
                ecb.AddComponent<UnitStartedMovingTag>(unit);
                ecb.SetComponentEnabled<UnitStartedMovingTag>(unit, false);
                ecb.AddComponent<UnitArrivedAtSlotTag>(unit);
                ecb.SetComponentEnabled<UnitArrivedAtSlotTag>(unit, false);

                // Combat engagement tag — enabled by UnitTargetingSystem when unit has a target
                ecb.AddComponent<IsEngagingTag>(unit);
                ecb.SetComponentEnabled<IsEngagingTag>(unit, false);

                // Retaliation signal — enabled by DamageCalculationSystem when unit is hit
                ecb.AddComponent<IsUnderAttackTag>(unit);
                ecb.SetComponentEnabled<IsUnderAttackTag>(unit, false);

                // Tactical stance (starts Normal; FormationStanceSystem updates on arrival)
                ecb.AddComponent(unit, new UnitFormationStanceComponent { stance = UnitStance.Normal });

                ecb.AddComponent(unit, new UnitGridSlotComponent
                {
                    gridPosition = originalGridPos,
                    slotIndex = i,
                    worldOffset = gridOffset
                });
                ecb.AddComponent(unit, new UnitOrientationComponent
                {
                    orientationType = UnitOrientationType.MatchHeroDirection,
                    rotationSpeed = spawnConfig.unitRotationSpeed
                });
                float maxSpeed = finalSpeed;
                // Asignar UnitAnimationMovementComponent por defecto con velocidad correcta
                ecb.AddComponent(unit, new ConquestTactics.Animation.UnitAnimationMovementComponent
                {
                    CurrentSpeed = 0f,
                    MaxSpeed = maxSpeed,
                    MovementDirection = new Unity.Mathematics.float3(0, 0, 1),
                    IsMoving = false,
                    IsRunning = false,
                    MovementTime = 0f,
                    StoppedTime = 0f,
                    PreviousPosition = worldPos, // Inicializar con la posición de spawn
                    CurrentStance = UnitStance.Normal,
                    SlotRow = originalGridPos.y,
                });
                // Agregar UnitStatsComponent usando los valores del SquadData
                ecb.AddComponent(unit, new UnitStatsComponent
                {
                    health = data.baseHealth,
                    speed = finalSpeed,
                    mass = data.mass,
                    weight = data.weight,
                    block = data.block,
                    slashingDefense = data.slashingDefense,
                    piercingDefense = data.piercingDefense,
                    bluntDefense = data.bluntDefense,
                    slashingDamage = data.slashingDamage,
                    piercingDamage = data.piercingDamage,
                    bluntDamage = data.bluntDamage,
                    slashingPenetration = data.slashingPenetration,
                    piercingPenetration = data.piercingPenetration,
                    bluntPenetration = data.bluntPenetration
                });
                ecb.AddComponent(unit, new HealthComponent
                {
                    maxHealth = data.baseHealth,
                    currentHealth = data.baseHealth
                });
                ecb.AddComponent(unit, new DefenseComponent
                {
                    bluntDefense  = data.bluntDefense,
                    slashDefense  = data.slashingDefense,
                    pierceDefense = data.piercingDefense
                });
                ecb.AddComponent(unit, new PenetrationComponent
                {
                    bluntPenetration  = data.bluntPenetration,
                    slashPenetration  = data.slashingPenetration,
                    piercePenetration = data.piercingPenetration
                });
                ecb.AddComponent(unit, new UnitWeaponComponent
                {
                    damageProfile           = squadDamageProfile,
                    attackRange             = data.attackRange,
                    attackInterval          = data.attackInterval,
                    criticalChance          = data.criticalChance,
                    criticalMultiplier      = data.criticalMultiplier,
                    strikeWindowStart       = data.strikeWindowStart,
                    strikeWindowDuration    = data.strikeWindowDuration,
                    attackAnimationDuration = data.attackAnimationDuration,
                    kineticMultiplier       = data.kineticMultiplier
                });

                // [Sprint4] Rotation intent — consumed and reset each frame by UnitRotationResolutionSystem
                ecb.AddComponent<UnitRotationIntentComponent>(unit);

                // ── Weapon hitbox gate tag — activated by UnitAttackSystem during strike window ──
                // Actual collision detection is done by WeaponHitboxBehaviour (GO BoxCollider trigger)
                // placed by the designer on the "WeaponHitbox" child GO of the unit prefab.
                ecb.AddComponent<WeaponHitboxActiveTag>(unit);
                ecb.SetComponentEnabled<WeaponHitboxActiveTag>(unit, false);

                ecb.AddBuffer<UnitDetectedEnemy>(unit);
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
            // Añadir HeroStateComponent al héroe (no es destructivo, solo se sobrescribe si ya existe)
            ecb.AddComponent(entity, new HeroStateComponent { State = HeroState.Idle });

            // Initialize InactiveSquadElement buffer for squad swap (only on first spawn, when buffer doesn't exist yet)
            if (!SystemAPI.HasBuffer<InactiveSquadElement>(entity))
            {
                var inactiveBuffer = ecb.AddBuffer<InactiveSquadElement>(entity);
                // Read the SquadIdMapElement buffer from DataContainer to get int→string mapping
                var dcQuery = GetEntityQuery(ComponentType.ReadOnly<DataContainerComponent>());
                if (!dcQuery.IsEmptyIgnoreFilter)
                {
                    Entity dcEntity = dcQuery.GetSingletonEntity();
                    if (SystemAPI.HasBuffer<SquadIdMapElement>(dcEntity))
                    {
                        var mapBuffer = SystemAPI.GetBuffer<SquadIdMapElement>(dcEntity);
                        var idLookup = GetComponentLookup<SquadDataIDComponent>(true);

                        for (int m = 0; m < mapBuffer.Length; m++)
                        {
                            var map = mapBuffer[m];
                            // Skip the currently active squad
                            if (map.squadId == selection.ValueRO.instanceId)
                                continue;

                            // Find the SquadDataIDComponent entity to get unit count
                            int totalUnitsForSquad = unitCount; // fallback
                            foreach (var (idComp, defComp) in SystemAPI
                                         .Query<RefRO<SquadDataIDComponent>, RefRO<SquadDefinitionComponent>>())
                            {
                                if (idComp.ValueRO.id == map.baseSquadID)
                                {
                                    totalUnitsForSquad = defComp.ValueRO.formationLibrary.Value.formations[0].gridPositions.Length;
                                    break;
                                }
                            }

                            inactiveBuffer.Add(new InactiveSquadElement
                            {
                                squadId = map.squadId,
                                baseSquadID = map.baseSquadID,
                                aliveUnits = totalUnitsForSquad,
                                totalUnits = totalUnitsForSquad,
                                isEliminated = false
                            });
                        }
                    }
                }
            }
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }

    /// <summary>
    /// Obtiene el nombre del prefab visual para un tipo de unidad.
    /// Los squads no tienen prefabs visuales, solo las unidades individuales.
    /// </summary>
    /// <param name="squadType">Tipo de squad para determinar el tipo de unidad</param>
    /// <returns>Nombre del prefab visual</returns>
    private FixedString64Bytes GetUnitVisualPrefabName(SquadType squadType)
    {
        return squadType switch
        {
            SquadType.Squires => "UnitVisual_Escudero",
            SquadType.Archers => "UnitVisual_Arquero",
            SquadType.Pikemen => "UnitVisual_Pikemen",
            SquadType.Spearmen => "UnitVisual_Spearmen",
            _ => "UnitVisual_Default"
        };
    }
}
