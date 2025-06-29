using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

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
        var formationLookup = GetComponentLookup<SquadFormationDataComponent>(true);
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (spawn, selection, transform, hero, entity) in SystemAPI
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
            if (!formationLookup.TryGetComponent(selection.ValueRO.squadDataEntity, out var formationData))
            {
                continue;
            }

            // Create squad entity
            Entity squad = ecb.CreateEntity();
            ecb.AddComponent(squad, new SquadOwnerComponent { hero = entity });
            ecb.AddComponent(squad, new SquadStatsComponent
            {
                squadType = data.squadType,
                behaviorProfile = data.behaviorProfile
            });
            ecb.AddComponent(squad, new SquadFormationDataComponent
            {
                formationLibrary = formationData.formationLibrary
            });
            ecb.AddComponent(squad, new SquadProgressComponent
            {
                level = 1,
                currentXP = 0f,
                xpToNextLevel = 100f
            });
            ecb.AddComponent(squad, new SquadInstanceComponent { id = selection.ValueRO.instanceId });

            var unitBuffer = ecb.AddBuffer<SquadUnitElement>(squad);

            ref var formations = ref formationData.formationLibrary.Value.formations;
            ref var offsets = ref formations[0].localOffsets;
            int unitCount = offsets.Length;

            for (int i = 0; i < unitCount; i++)
            {
                if (data.unitPrefab == Entity.Null)
                {
                    break;
                }

                Entity unit = ecb.Instantiate(data.unitPrefab);
                float3 offset = offsets[i];
                float3 baseXZ = transform.ValueRO.Position + new float3(offset.x, 0, offset.z);

                // Obtener altura del terreno clásico de Unity
                float y = 0f;
                if (UnityEngine.Terrain.activeTerrain != null)
                {
                    y = UnityEngine.Terrain.activeTerrain.SampleHeight(new UnityEngine.Vector3(baseXZ.x, 0, baseXZ.z));
                    y += UnityEngine.Terrain.activeTerrain.GetPosition().y; // Ajuste por la posición del terreno
                }
                float3 worldPos = new float3(baseXZ.x, y, baseXZ.z);

                ecb.SetComponent(unit, LocalTransform.FromPosition(worldPos));
                ecb.AddComponent(unit, new UnitFormationSlotComponent
                {
                    relativeOffset = offsets[i],
                    slotIndex = i
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
                unitBuffer.Add(new SquadUnitElement { Value = unit });
            }
            ecb.AddComponent(entity, new HeroSquadReference { squad = squad });
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
