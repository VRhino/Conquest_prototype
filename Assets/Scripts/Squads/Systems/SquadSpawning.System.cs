using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
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
    }

    protected override void OnUpdate()
    {
        Dependency.Complete();
        var spawnConfig = SystemAPI.GetSingleton<SquadSpawnConfigComponent>();
        var dataLookup = GetComponentLookup<SquadDataComponent>(true);
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
            {
                continue;
            }
            // Create squad entity (ECS-only, sin visuales)
            Entity squad = ecb.CreateEntity();

            // Configurar posición inicial del squad offset delante del héroe
            quaternion heroRotation = heroTransform.ValueRO.Rotation;
            float3 heroForward = math.forward(heroRotation);
            float3 formationAnchor = heroTransform.ValueRO.Position + heroForward * spawnConfig.squadSpawnOffset;
            ecb.AddComponent(squad, LocalTransform.FromPosition(formationAnchor));

            ecb.AddComponent(squad, new SquadOwnerComponent { hero = entity });
            // Squad created successfully

            ecb.AddComponent(squad, data); // Add the complete SquadDataComponent
            ecb.AddComponent(squad, new SquadStatsComponent
            {
                squadType = data.squadType,
                behaviorProfile = data.behaviorProfile
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
            var firstFormationType = data.formationLibrary.Value.formations.Length > 0
                ? data.formationLibrary.Value.formations[0].formationType
                : FormationType.Line; // Fallback en caso de que no haya formaciones (edge case)

            // Héroes remotos comienzan en HoldPosition para evitar movimiento errático sin IA
            bool isLocalHero = SystemAPI.HasComponent<IsLocalPlayer>(entity);
            var initialOrder = isLocalHero ? SquadOrderType.FollowHero : SquadOrderType.HoldPosition;
            var initialState = isLocalHero ? SquadFSMState.FollowingHero : SquadFSMState.HoldingPosition;

            // Fix: squads remotos necesitan SquadHoldPositionComponent para que
            // GridFormationUpdateSystem use holdCenter fijo en lugar de seguir al héroe
            if (!isLocalHero)
            {
                ecb.AddComponent(squad, new SquadHoldPositionComponent
                {
                    holdCenter = formationAnchor,
                    holdRotation = heroRotation,
                    originalFormation = firstFormationType
                });
            }

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
                currentFormation = firstFormationType, // Formación inicial: primera del arreglo
                currentOrder = initialOrder,
                isExecutingOrder = false,
                isInCombat = false,
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

            // Buffers de combate y detección de enemigos
            ecb.AddComponent<SquadAIComponent>(squad);
            ecb.AddBuffer<DetectedEnemy>(squad);
            ecb.AddBuffer<SquadTargetEntity>(squad);

            // Create one damage profile entity per squad — all units reference it.
            Entity squadDamageProfile = ecb.CreateEntity();
            ecb.AddComponent(squadDamageProfile, new DamageProfileComponent
            {
                baseDamage  = GetPrimaryDamage(data),
                damageType  = GetPrimaryDamageType(data.squadType),
                penetration = GetPrimaryPenetration(data)
            });

            var unitBuffer = ecb.AddBuffer<SquadUnitElement>(squad);

            ref var formations = ref data.formationLibrary.Value.formations;
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

            for (int i = 0; i < spawnCount; i++)
            {
                // Crear unidad ECS (solo lógica, sin visuales)
                Entity unit = ecb.CreateEntity();
                FormationPositionCalculator.CalculateDesiredPosition(
                    unit,
                    ref firstFormation.gridPositions,
                    i, // unitIndex
                    new SquadStateComponent { currentFormation = firstFormationType },
                    null,
                    heroTransform.ValueRO.Position, // misma base que GridFormationUpdateSystem (sin squadSpawnOffset)
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
                    visualPrefabName = GetUnitVisualPrefabName(data.squadType)
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
                // Obtener velocidad máxima desde el SquadDataComponent (todas las unidades del squad comparten la misma velocidad base)
                float maxSpeed = data.baseSpeed;
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
                    PreviousPosition = worldPos // Inicializar con la posición de spawn
                });
                // Agregar UnitStatsComponent usando los valores del SquadData
                ecb.AddComponent(unit, new UnitStatsComponent
                {
                    health = data.baseHealth,
                    speed = data.baseSpeed,
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
                    damageZoneStart         = data.damageZoneStart,
                    damageZoneHalfWidth     = data.damageZoneHalfWidth,
                    damageZoneYOffset       = data.damageZoneYOffset,
                    damageZoneHalfHeight    = data.damageZoneHalfHeight,
                    strikeWindowStart       = data.strikeWindowStart,
                    strikeWindowDuration    = data.strikeWindowDuration,
                    attackAnimationDuration = data.attackAnimationDuration,
                    kineticMultiplier       = data.kineticMultiplier
                });

                // ── Hurtbox: permanent capsule collider on unit (receives damage) ──────
                // Layer 6 = Hurtbox. Configure in Unity Physics Category Names.
                var hurtboxCollider = Unity.Physics.CapsuleCollider.Create(
                    new CapsuleGeometry
                    {
                        Vertex0 = new float3(0f, 0.4f, 0f),
                        Vertex1 = new float3(0f, 1.6f, 0f),
                        Radius  = 0.35f
                    },
                    new CollisionFilter
                    {
                        BelongsTo    = PhysicsLayers.HurtboxMask,
                        CollidesWith = PhysicsLayers.HitboxMask,
                        GroupIndex   = 0
                    });
                ecb.AddComponent(unit, new PhysicsCollider { Value = hurtboxCollider });

                // ── Weapon hitbox entity: separate ECS entity, disabled by default ─────
                // Activated only during the strike window by UnitAttackSystem.
                float hitboxHalfD = math.max(0.01f,
                    (data.attackRange - data.damageZoneStart) * 0.5f);
                float sizeX = math.max(0.02f, data.damageZoneHalfWidth  * 2f);
                float sizeY = math.max(0.02f, data.damageZoneHalfHeight * 2f);
                float sizeZ = math.max(0.02f, hitboxHalfD               * 2f);
                float bevelRadius = math.min(0.01f, math.min(sizeX, math.min(sizeY, sizeZ)) * 0.49f);
                var weaponHitboxCollider = Unity.Physics.BoxCollider.Create(
                    new BoxGeometry
                    {
                        Center      = float3.zero,
                        Orientation = quaternion.identity,
                        Size        = new float3(sizeX, sizeY, sizeZ),
                        BevelRadius = bevelRadius
                    },
                    new CollisionFilter
                    {
                        BelongsTo    = PhysicsLayers.HitboxMask,
                        CollidesWith = PhysicsLayers.HurtboxMask,
                        GroupIndex   = 0
                    });
                Entity hitboxEntity = ecb.CreateEntity();
                ecb.AddComponent(hitboxEntity, LocalTransform.FromPosition(worldPos));
                ecb.AddComponent(hitboxEntity, new PhysicsCollider { Value = weaponHitboxCollider });
                ecb.AddComponent(hitboxEntity, new WeaponHitboxOwner { ownerUnit = unit });
                ecb.AddComponent(hitboxEntity, new WeaponHitboxActiveTag());
                ecb.SetComponentEnabled<WeaponHitboxActiveTag>(hitboxEntity, false);
                ecb.AddComponent(unit, new WeaponHitboxRef { hitboxEntity = hitboxEntity });

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
                            foreach (var (idComp, dataComp) in SystemAPI
                                         .Query<RefRO<SquadDataIDComponent>, RefRO<SquadDataComponent>>())
                            {
                                if (idComp.ValueRO.id == map.baseSquadID)
                                {
                                    totalUnitsForSquad = dataComp.ValueRO.formationLibrary.Value.formations[0].gridPositions.Length;
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

    private static float GetPrimaryDamage(SquadDataComponent data)
    {
        return data.squadType switch
        {
            SquadType.Archers  => data.piercingDamage,
            SquadType.Pikemen  => data.piercingDamage,
            SquadType.Spearmen => data.piercingDamage,
            _                  => data.slashingDamage   // Squires and default
        };
    }

    private static DamageType GetPrimaryDamageType(SquadType squadType)
    {
        return squadType switch
        {
            SquadType.Archers  => DamageType.Piercing,
            SquadType.Pikemen  => DamageType.Piercing,
            SquadType.Spearmen => DamageType.Piercing,
            _                  => DamageType.Slashing  // Squires and default
        };
    }

    private static float GetPrimaryPenetration(SquadDataComponent data)
    {
        return data.squadType switch
        {
            SquadType.Archers  => data.piercingPenetration,
            SquadType.Pikemen  => data.piercingPenetration,
            SquadType.Spearmen => data.piercingPenetration,
            _                  => data.slashingPenetration
        };
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
