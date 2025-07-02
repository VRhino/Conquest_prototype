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

            // Create squad entity
            Entity squad = ecb.CreateEntity();
            ecb.AddComponent(squad, new SquadOwnerComponent { hero = entity });
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
            
            // Obtener la primera formación disponible en el arreglo como formación por defecto (índice 0)
            var firstFormationType = data.formationLibrary.Value.formations.Length > 0 
                ? data.formationLibrary.Value.formations[0].formationType 
                : FormationType.Line; // Fallback en caso de que no haya formaciones (edge case)
            
            // Agregar componentes necesarios para el sistema de control y formación
            ecb.AddComponent(squad, new SquadInputComponent
            {
                orderType = SquadOrderType.FollowHero,
                hasNewOrder = false,
                desiredFormation = firstFormationType // Formación inicial: primera del arreglo
            });
            
            ecb.AddComponent(squad, new SquadStateComponent
            {
                currentFormation = firstFormationType, // Formación inicial: primera del arreglo
                currentOrder = SquadOrderType.FollowHero,
                isExecutingOrder = false,
                isInCombat = false,
                formationChangeCooldown = 0f,
                currentState = SquadFSMState.Idle,
                transitionTo = SquadFSMState.Idle,
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
                if (data.unitPrefab == Entity.Null)
                {
                    break;
                }

                Entity unit = ecb.Instantiate(data.unitPrefab);
                FormationPositionCalculator.CalculateDesiredPosition(unit, ref firstFormation.gridPositions, heroTransform.ValueRO.Position, i, out int2 originalGridPos, out float3 gridOffset, out float3 worldPos, true);

                ecb.SetComponent(unit, LocalTransform.FromPosition(worldPos));
                
                // Use the grid system - mantener la posición original para gridPosition
                ecb.AddComponent(unit, new UnitGridSlotComponent
                {
                    gridPosition = originalGridPos, // Mantener posición original del ScriptableObject
                    slotIndex = i,
                    worldOffset = gridOffset // Usar offset centrado para posicionamiento
                });
                // Inicializar el campo Slot de UnitSpacingComponent
                ecb.SetComponent(unit, new UnitSpacingComponent {
                    minDistance = /* valor adecuado, por ejemplo 1.5f o el que corresponda */ 1.5f,
                    repelForce = /* valor adecuado, por ejemplo 1f o el que corresponda */ 1f,
                    Slot = originalGridPos // Usar posición original para Slot
                });
                
                // Add UnitTargetPositionComponent from the start
                ecb.AddComponent(unit, new UnitTargetPositionComponent
                {
                    position = worldPos
                });
                ecb.AddComponent(unit, new UnitOwnerComponent { squad = squad, hero = entity });
                ecb.AddComponent(unit, new UnitStatsComponent
                {
                    vida = data.vidaBase,
                    velocidad = data.velocidadBase,
                    masa = data.masa,
                    peso = (int)data.peso,
                    bloqueo = data.bloqueo,
                    defensaCortante = data.defensaCortante,
                    defensaPerforante = data.defensaPerforante,
                    defensaContundente = data.defensaContundente,
                    danoCortante = data.danoCortante,
                    danoPerforante = data.danoPerforante,
                    danoContundente = data.danoContundente,
                    penetracionCortante = data.penetracionCortante,
                    penetracionPerforante = data.penetracionPerforante,
                    penetracionContundente = data.penetracionContundente,
                    liderazgoCosto = data.liderazgoCost
                });
                if (data.esUnidadADistancia)
                {
                    ecb.AddComponent(unit, new UnitRangedStatsComponent
                    {
                        alcance = data.alcance,
                        precision = data.precision,
                        cadenciaFuego = data.cadenciaFuego,
                        velocidadRecarga = data.velocidadRecarga,
                        municionTotal = data.municionTotal
                    });
                }
                // Añadir delay de seguimiento aleatorio a cada unidad (nuevo comportamiento)
                float randomDelay = UnityEngine.Random.Range(0.5f, 1.5f);
                ecb.AddComponent(unit, new UnitFollowDelayComponent {
                    delay = randomDelay,
                    timer = 0f,
                    waiting = false,
                    triggered = false
                });
                // Añadir posición previa del líder para evitar error de acceso
                ecb.AddComponent(unit, new UnitPrevLeaderPosComponent {
                    value = worldPos
                });
                // Variación de velocidad individual
                float speedMultiplier = UnityEngine.Random.Range(0.9f, 1.1f);
                ecb.AddComponent(unit, new UnitMoveSpeedVariation {
                    speedMultiplier = speedMultiplier
                });
                // Añadir UnitFormationStateComponent a cada unidad
                ecb.AddComponent(unit, new UnitFormationStateComponent {
                    State = UnitFormationState.Formed,
                    DelayTimer = 0f,
                    DelayDuration = 0f
                });
                unitBuffer.Add(new SquadUnitElement { Value = unit });
            }
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
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
