using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// MonoBehaviour used to bake formation data from scriptable objects into a blob asset.
/// </summary>
public class SquadFormationDataAuthoring : MonoBehaviour
{
    public FormationScriptableObject[] formations;

    class SquadFormationDataBaker : Unity.Entities.Baker<SquadFormationDataAuthoring>
    {
        public override void Bake(SquadFormationDataAuthoring authoring)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<FormationLibraryBlob>();
            var formationArray = builder.Allocate(ref root.formations, authoring.formations.Length);
            for (int i = 0; i < authoring.formations.Length; i++)
            {
                var form = authoring.formations[i];
                formationArray[i].formationType = form.formationType;
                var offsets = builder.Allocate(ref formationArray[i].localOffsets, form.localOffsets.Length);
                for (int j = 0; j < form.localOffsets.Length; j++)
                    offsets[j] = form.localOffsets[j];
            }

            var blob = builder.CreateBlobAssetReference<FormationLibraryBlob>(Allocator.Persistent);
            builder.Dispose();

            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new SquadFormationDataComponent { formationLibrary = blob });
        }
    }
}
