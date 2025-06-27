using Unity.Entities;
using UnityEngine;

/// <summary>
/// MonoBehaviour used on squad prefabs to assign the corresponding <see cref="SquadData"/>.
/// </summary>
public class SquadDataReferenceAuthoring : MonoBehaviour
{
    public SquadData squadData;

    class Baker : Unity.Entities.Baker<SquadDataReferenceAuthoring>
    {
        public override void Bake(SquadDataReferenceAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            if (authoring.squadData == null)
                return;

            var dataEntity = GetEntity(authoring.squadData, TransformUsageFlags.None);
            AddComponent(entity, new SquadDataReference { dataEntity = dataEntity });
        }
    }
}

