using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Bakes all squad types from a <see cref="SquadDatabase"/> into ECS entities at once.
/// Place ONE instance of this MonoBehaviour in DOTSWorld.unity and assign the SquadDatabase
/// asset so every squad type is available for runtime lookup via <see cref="SquadDataIDComponent"/>.
/// </summary>
public class SquadDatabaseAuthoring : MonoBehaviour
{
    public SquadDatabase database;

    class Baker : Unity.Entities.Baker<SquadDatabaseAuthoring>
    {
        public override void Bake(SquadDatabaseAuthoring authoring)
        {
            if (authoring.database == null || authoring.database.allSquads == null)
                return;

            foreach (var squadData in authoring.database.allSquads)
            {
                if (squadData == null || string.IsNullOrEmpty(squadData.id))
                    continue;

                var entity = CreateAdditionalEntity(TransformUsageFlags.None, false, squadData.id);

                Entity prefabEntity = squadData.prefab != null
                    ? GetEntity(squadData.prefab, TransformUsageFlags.Dynamic)
                    : Entity.Null;

                var formationLibrary = BakeFormationLibrary(squadData.gridFormations);

                AddComponent(entity, new SquadDataComponent
                {
                    baseHealth = squadData.baseHealth,
                    baseSpeed = squadData.baseSpeed,
                    mass = squadData.massValue,
                    weight = squadData.totalWeight,
                    squadType = squadData.type,
                    block = squadData.block,
                    slashingDefense = squadData.slashingDefense,
                    piercingDefense = squadData.piercingDefense,
                    bluntDefense = squadData.bluntDefense,
                    slashingDamage = squadData.slashingDamage,
                    piercingDamage = squadData.piercingDamage,
                    bluntDamage = squadData.bluntDamage,
                    slashingPenetration = squadData.slashingPenetration,
                    piercingPenetration = squadData.piercingPenetration,
                    bluntPenetration = squadData.bluntPenetration,
                    isRangedUnit = squadData.isDistanceUnit,
                    range = squadData.range,
                    accuracy = squadData.accuracy,
                    fireRate = squadData.fireRate,
                    reloadSpeed = squadData.reloadSpeed,
                    ammoCapacity = squadData.ammo,
                    leadershipCost = squadData.leadershipCost,
                    behaviorProfile = squadData.behaviorProfile,
                    curves = default,
                    formationLibrary = formationLibrary,
                    unitPrefab = prefabEntity,
                    unitCount = squadData.unitCount,
                    attackRange = squadData.attackRange,
                    attackInterval = squadData.attackInterval,
                    criticalChance = squadData.criticalChance,
                    criticalMultiplier = squadData.criticalMultiplier,
                    detectionRange = squadData.detectionRange,
                    strikeWindowStart = squadData.strikeWindowStart,
                    strikeWindowDuration = squadData.strikeWindowDuration,
                    attackAnimationDuration = squadData.attackAnimationDuration,
                    kineticMultiplier = squadData.kineticMultiplier
                });

                AddComponent(entity, new SquadDataIDComponent { id = new FixedString64Bytes(squadData.id) });

                AddComponent(entity, new SquadStatsComponent
                {
                    squadType = squadData.type,
                    behaviorProfile = squadData.behaviorProfile
                });

                var statsBuffer = AddBuffer<UnitStatsBufferElement>(entity);
                statsBuffer.Add(new UnitStatsBufferElement
                {
                    health = (int)squadData.baseHealth,
                    speed = (int)squadData.baseSpeed,
                    mass = (int)squadData.massValue,
                    weightClass = (int)squadData.totalWeight,
                    blockValue = squadData.block,
                    slashingDamage = squadData.slashingDamage,
                    piercingDamage = squadData.piercingDamage,
                    bluntDamage = squadData.bluntDamage,
                    slashingDefense = squadData.slashingDefense,
                    piercingDefense = squadData.piercingDefense,
                    bluntDefense = squadData.bluntDefense,
                    leadershipCost = squadData.leadershipCost
                });
            }
        }

        private BlobAssetReference<FormationLibraryBlob> BakeFormationLibrary(GridFormationScriptableObject[] gridFormations)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<FormationLibraryBlob>();

            if (gridFormations == null || gridFormations.Length == 0)
            {
                builder.Allocate(ref root.formations, 0);
            }
            else
            {
                var formationArray = builder.Allocate(ref root.formations, gridFormations.Length);

                for (int i = 0; i < gridFormations.Length; i++)
                {
                    var gridForm = gridFormations[i];
                    if (gridForm == null)
                        continue;

                    formationArray[i].formationType = gridForm.formationType;

                    Vector2Int[] originalPositions = gridForm.gridPositions;
                    var gridPositions = builder.Allocate(ref formationArray[i].gridPositions, originalPositions.Length);
                    for (int j = 0; j < originalPositions.Length; j++)
                        gridPositions[j] = new int2(originalPositions[j].x, originalPositions[j].y);
                }
            }

            var blob = builder.CreateBlobAssetReference<FormationLibraryBlob>(Allocator.Persistent);
            builder.Dispose();
            return blob;
        }
    }
}
