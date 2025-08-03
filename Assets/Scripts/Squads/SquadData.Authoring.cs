using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// MonoBehaviour used to bake <see cref="SquadData"/> assets into ECS components
/// and buffers.
/// </summary>
public class SquadDataAuthoring : MonoBehaviour
{
    public SquadData data;

    class SquadDataBaker : Unity.Entities.Baker<SquadDataAuthoring>
    {
        public override void Bake(SquadDataAuthoring authoring)
        {
            if (authoring.data == null)
                return;

            var entity = GetEntity(TransformUsageFlags.None);

            Entity prefabEntity = authoring.data.prefab != null
                ? GetEntity(authoring.data.prefab, TransformUsageFlags.Dynamic)
                : Entity.Null;

            // Bake formation library
            var formationLibrary = BakeFormationLibrary(authoring.data.gridFormations);

            AddComponent(entity, new SquadDataComponent
            {
                vidaBase = authoring.data.baseHealth,
                velocidadBase = authoring.data.baseSpeed,
                masa = authoring.data.massValue,
                peso = authoring.data.totalWeight,
                squadType = authoring.data.type,
                bloqueo = authoring.data.block,
                defensaCortante = authoring.data.slashingDefense,
                defensaPerforante = authoring.data.piercingDefense,
                defensaContundente = authoring.data.bluntDefense,
                danoCortante = authoring.data.slashingDamage,
                danoPerforante = authoring.data.piercingDamage,
                danoContundente = authoring.data.bluntDamage,
                penetracionCortante = authoring.data.slashingPenetration,
                penetracionPerforante = authoring.data.piercingPenetration,
                penetracionContundente = authoring.data.bluntPenetration,
                esUnidadADistancia = authoring.data.isDistanceUnit,
                alcance = authoring.data.range,
                precision = authoring.data.accuracy,
                cadenciaFuego = authoring.data.fireRate,
                velocidadRecarga = authoring.data.reloadSpeed,
                municionTotal = authoring.data.ammo,
                liderazgoCost = authoring.data.leadershipCost,
                behaviorProfile = authoring.data.behaviorProfile,
                curves = default,
                formationLibrary = formationLibrary,
                unitPrefab = prefabEntity,
                unitCount = authoring.data.unitCount
            });

            AddComponent(entity, new SquadStatsComponent
            {
                squadType = authoring.data.type,
                behaviorProfile = authoring.data.behaviorProfile
            });

            var statsBuffer = AddBuffer<UnitStatsBufferElement>(entity);
            statsBuffer.Add(new UnitStatsBufferElement
            {
                health = (int)authoring.data.baseHealth,
                speed = (int)authoring.data.baseSpeed,
                mass = (int)authoring.data.massValue,
                weightClass = (int)authoring.data.totalWeight,
                blockValue = authoring.data.block,
                slashingDamage = authoring.data.slashingDamage,
                piercingDamage = authoring.data.piercingDamage,
                bluntDamage = authoring.data.bluntDamage,
                slashingDefense = authoring.data.slashingDefense,
                piercingDefense = authoring.data.piercingDefense,
                bluntDefense = authoring.data.bluntDefense,
                leadershipCost = authoring.data.leadershipCost
            });
        }

        private BlobAssetReference<FormationLibraryBlob> BakeFormationLibrary(GridFormationScriptableObject[] gridFormations)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<FormationLibraryBlob>();
            
            if (gridFormations == null || gridFormations.Length == 0)
            {
                // No grid formations assigned to squad data
                builder.Allocate(ref root.formations, 0);
            }
            else
            {
                var formationArray = builder.Allocate(ref root.formations, gridFormations.Length);
                
                for (int i = 0; i < gridFormations.Length; i++)
                {
                    var gridForm = gridFormations[i];
                    if (gridForm == null)
                    {
                        // Grid formation is null, skipping
                        continue;
                    }

                    formationArray[i].formationType = gridForm.formationType;
                    
                    // Store original grid positions from ScriptableObject
                    Vector2Int[] originalPositions = gridForm.gridPositions;
                    var gridPositions = builder.Allocate(ref formationArray[i].gridPositions, originalPositions.Length);
                    for (int j = 0; j < originalPositions.Length; j++)
                    {
                        gridPositions[j] = new int2(originalPositions[j].x, originalPositions[j].y);
                    }
                }
            }

            var blob = builder.CreateBlobAssetReference<FormationLibraryBlob>(Allocator.Persistent);
            builder.Dispose();
            return blob;
        }
    }
}
