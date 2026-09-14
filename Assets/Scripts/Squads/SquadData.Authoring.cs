using Unity.Entities;
using UnityEngine;

public class SquadDataAuthoring : MonoBehaviour
{
    public SquadData data;

    class SquadDataBaker : SquadDefinitionBaker<SquadDataAuthoring>
    {
        public override void Bake(SquadDataAuthoring authoring)
        {
            if (authoring.data != null)
                BakeSquad(GetEntity(TransformUsageFlags.None), authoring.data);
        }
    }
}
