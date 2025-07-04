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
    protected override void OnUpdate()
    {
        Dependency.Complete();
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
            
            // Configurar posición inicial del squad
            ecb.AddComponent(squad, LocalTransform.FromPosition(heroTransform.ValueRO.Position));
            
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
            
            // Agregar componentes necesarios para el sistema de control y formación
            ecb.AddComponent(squad, new SquadInputComponent
            {
                orderType = SquadOrderType.FollowHero,
                hasNewOrder = false,
                desiredFormation = firstFormationType, // Formación inicial: primera del arreglo
                holdPosition = float3.zero // Inicializar con posición vacía
            });
            
            ecb.AddComponent(squad, new SquadStateComponent
            {
                currentFormation = firstFormationType, // Formación inicial: primera del arreglo
                currentOrder = SquadOrderType.FollowHero,
                isExecutingOrder = false,
                isInCombat = false,
                formationChangeCooldown = 0f,
                currentState = SquadFSMState.FollowingHero, // Initial state should match the initial order
                transitionTo = SquadFSMState.FollowingHero, // Initial state should match the initial order
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

            var unitBuffer = ecb.AddBuffer<SquadUnitElement>(squad);

            ref var formations = ref data.formationLibrary.Value.formations;
            ref var firstFormation = ref formations[0]; // Always use index 0 for initial spawn
            
            int unitCount = firstFormation.gridPositions.Length;

            for (int i = 0; i < unitCount; i++)
            {
                // Crear unidad ECS (solo lógica, sin visuales)
                Entity unit = ecb.CreateEntity();
                FormationPositionCalculator.CalculateDesiredPosition(
                    unit,
                    ref firstFormation.gridPositions,
                    i, // unitIndex
                    new SquadStateComponent { currentFormation = firstFormationType },
                    null,
                    heroTransform.ValueRO.Position,
                    out int2 originalGridPos,
                    out float3 gridOffset,
                    out float3 worldPos,
                    true);

                ecb.AddComponent(unit, LocalTransform.FromPosition(worldPos));
                Debug.Log($"[SquadSpawningSystem.cs][{unit}] Set LocalTransform.Position = {worldPos}");
                
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
                    minDistance = 1.5f,
                    repelForce = 1f,
                    Slot = originalGridPos // Usar posición original para Slot
                });
                ecb.AddComponent<UnitTargetPositionComponent>(unit);
                ecb.AddComponent<UnitFormationStateComponent>(unit); // Critical component for movement
                ecb.AddComponent(unit, new UnitGridSlotComponent
                {
                    gridPosition = originalGridPos,
                    slotIndex = i,
                    worldOffset = gridOffset
                });
                ecb.AddComponent(unit, new UnitOrientationComponent
                {
                    orientationType = UnitOrientationType.MatchHeroDirection,
                    rotationSpeed = 5f
                });
                // Asignar UnitAnimationMovementComponent por defecto
                ecb.AddComponent(unit, new ConquestTactics.Animation.UnitAnimationMovementComponent
                {
                    CurrentSpeed = 0f,
                    MaxSpeed = 5f,
                    MovementDirection = new Unity.Mathematics.float3(0, 0, 1),
                    IsMoving = false,
                    IsRunning = false,
                    MovementTime = 0f,
                    StoppedTime = 0f,
                    PreviousPosition = worldPos // Inicializar con la posición de spawn
                });
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
            ecb.AddComponent(entity, new HeroStateComponent { State = HeroState.Idle });;
            
            // Squad ECS created successfully
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
            SquadType.Lancers => "UnitVisual_Caballo", 
            _ => "UnitVisual_Default"
        };
    }
}
