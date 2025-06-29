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
                continue;

            if (!dataLookup.TryGetComponent(selection.ValueRO.squadDataEntity, out var data) ||
                !formationLookup.TryGetComponent(selection.ValueRO.squadDataEntity, out var formationData))
                continue;

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
                    break;

                Entity unit = ecb.Instantiate(data.unitPrefab);
                float3 worldPos = transform.ValueRO.Position + offsets[i];
                ecb.SetComponent(unit, LocalTransform.FromPosition(worldPos));
                ecb.AddComponent(unit, new UnitFormationSlotComponent
                {
                    relativeOffset = offsets[i],
                    slotIndex = i
                });
                ecb.AddComponent(unit, new UnitOwnerComponent { squad = squad, hero = entity });
                unitBuffer.Add(new SquadUnitElement { Value = unit });
            }

            ecb.AddComponent(entity, new HeroSquadReference { squad = squad });
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
