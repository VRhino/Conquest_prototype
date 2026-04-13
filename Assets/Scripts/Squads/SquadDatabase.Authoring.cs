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

                var melee  = squadData.meleeData;
                var ranged = squadData.rangedData;

                bool  isRanged    = ranged != null;
                float slashDmg    = melee  != null ? melee.slashingDamage        : ranged?.slashingDamage        ?? 0f;
                float pierceDmg   = melee  != null ? melee.piercingDamage        : ranged?.piercingDamage        ?? 0f;
                float bluntDmg    = melee  != null ? melee.bluntDamage           : ranged?.bluntDamage           ?? 0f;
                float slashPen    = melee  != null ? melee.slashingPenetration   : ranged?.slashingPenetration   ?? 0f;
                float piercePen   = melee  != null ? melee.piercingPenetration   : ranged?.piercingPenetration   ?? 0f;
                float bluntPen    = melee  != null ? melee.bluntPenetration      : ranged?.bluntPenetration      ?? 0f;
                string poolKey    = ranged?.projectilePoolKey ?? string.Empty;

                AddComponent(entity, new SquadDataComponent
                {
                    baseHealth = squadData.baseHealth,
                    baseSpeed = squadData.baseSpeed,
                    mass = squadData.massValue,
                    weight = squadData.totalWeight,
                    block = squadData.block,
                    slashingDefense = squadData.slashingDefense,
                    piercingDefense = squadData.piercingDefense,
                    bluntDefense = squadData.bluntDefense,
                    slashingDamage = slashDmg,
                    piercingDamage = pierceDmg,
                    bluntDamage = bluntDmg,
                    slashingPenetration = slashPen,
                    piercingPenetration = piercePen,
                    bluntPenetration = bluntPen,
                    isRangedUnit = isRanged,
                    range            = ranged?.range        ?? 0f,
                    accuracy         = ranged?.accuracy     ?? 0f,
                    fireRate         = ranged?.fireRate     ?? 0f,
                    reloadSpeed      = ranged?.reloadSpeed  ?? 0f,
                    ammoCapacity     = ranged?.ammo         ?? 0,
                    projectilePoolKey = string.IsNullOrEmpty(poolKey)
                        ? default
                        : new FixedString32Bytes(poolKey),
                    projectileTrajectory = ranged?.projectileTrajectory ?? default,
                    curves = default,
                    attackRange             = melee?.attackRange             ?? 2f,
                    attackInterval          = melee?.attackInterval          ?? 1.5f,
                    criticalChance          = melee?.criticalChance          ?? 0.05f,
                    criticalMultiplier      = melee?.criticalMultiplier      ?? 1.5f,
                    strikeWindowStart       = melee?.strikeWindowStart       ?? 0.35f,
                    strikeWindowDuration    = melee?.strikeWindowDuration    ?? 0.15f,
                    attackAnimationDuration = melee?.attackAnimationDuration ?? 1.0f,
                    kineticMultiplier       = melee?.kineticMultiplier       ?? 0.3f
                });

                AddComponent(entity, new SquadDefinitionComponent
                {
                    squadType        = squadData.type,
                    behaviorProfile  = squadData.behaviorProfile,
                    formationLibrary = formationLibrary,
                    unitPrefab       = prefabEntity,
                    unitCount        = squadData.unitCount,
                    GridSize         = default,
                    leadershipCost   = squadData.leadershipCost,
                    detectionRange   = squadData.detectionRange
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
                    slashingDamage = slashDmg,
                    piercingDamage = pierceDmg,
                    bluntDamage = bluntDmg,
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
