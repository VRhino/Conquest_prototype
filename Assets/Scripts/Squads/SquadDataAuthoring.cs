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
                vidaBase = authoring.data.vidaBase,
                velocidadBase = authoring.data.velocidadBase,
                masa = authoring.data.masa,
                peso = authoring.data.peso,
                squadType = authoring.data.tipo,
                bloqueo = authoring.data.bloqueo,
                defensaCortante = authoring.data.defensaCortante,
                defensaPerforante = authoring.data.defensaPerforante,
                defensaContundente = authoring.data.defensaContundente,
                danoCortante = authoring.data.danoCortante,
                danoPerforante = authoring.data.danoPerforante,
                danoContundente = authoring.data.danoContundente,
                penetracionCortante = authoring.data.penetracionCortante,
                penetracionPerforante = authoring.data.penetracionPerforante,
                penetracionContundente = authoring.data.penetracionContundente,
                esUnidadADistancia = authoring.data.esUnidadADistancia,
                alcance = authoring.data.alcance,
                precision = authoring.data.precision,
                cadenciaFuego = authoring.data.cadenciaFuego,
                velocidadRecarga = authoring.data.velocidadRecarga,
                municionTotal = authoring.data.municionTotal,
                liderazgoCost = authoring.data.liderazgoCost,
                behaviorProfile = authoring.data.behaviorProfile,
                curves = default,
                formationLibrary = formationLibrary,
                unitPrefab = prefabEntity,
                unitCount = authoring.data.unitCount
            });

            AddComponent(entity, new SquadStatsComponent
            {
                squadType = authoring.data.tipo,
                behaviorProfile = authoring.data.behaviorProfile
            });

            var statsBuffer = AddBuffer<UnitStatsBufferElement>(entity);
            statsBuffer.Add(new UnitStatsBufferElement
            {
                health = (int)authoring.data.vidaBase,
                speed = (int)authoring.data.velocidadBase,
                mass = (int)authoring.data.masa,
                weightClass = (int)authoring.data.peso,
                blockValue = authoring.data.bloqueo,
                slashingDamage = authoring.data.danoCortante,
                piercingDamage = authoring.data.danoPerforante,
                bluntDamage = authoring.data.danoContundente,
                slashingDefense = authoring.data.defensaCortante,
                piercingDefense = authoring.data.defensaPerforante,
                bluntDefense = authoring.data.defensaContundente,
                leadershipCost = authoring.data.liderazgoCost
            });
        }

        private BlobAssetReference<FormationLibraryBlob> BakeFormationLibrary(GridFormationScriptableObject[] gridFormations)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<FormationLibraryBlob>();
            
            if (gridFormations == null || gridFormations.Length == 0)
            {
                Debug.LogWarning("No grid formations assigned to squad data. Creating empty formation library.");
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
                        Debug.LogWarning($"Grid formation at index {i} is null in squad data. Skipping.");
                        continue;
                    }

                    formationArray[i].formationType = gridForm.formationType;
                    
                    // Store centered grid positions (hero-relative)
                    Vector2Int[] centeredPositions = gridForm.GetCenteredGridPositions();
                    var gridPositions = builder.Allocate(ref formationArray[i].gridPositions, centeredPositions.Length);
                    for (int j = 0; j < centeredPositions.Length; j++)
                    {
                        gridPositions[j] = new int2(centeredPositions[j].x, centeredPositions[j].y);
                    }
                }
            }

            var blob = builder.CreateBlobAssetReference<FormationLibraryBlob>(Allocator.Persistent);
            builder.Dispose();
            return blob;
        }
    }
}
