using Unity.Entities;
using UnityEngine;

/// <summary>
/// MonoBehaviour used to bake <see cref="HeroPerk"/> assets into entities
/// with <see cref="HeroPerkComponent"/> so they can be referenced at runtime.
/// </summary>
public class HeroPerkAuthoring : MonoBehaviour
{
    public HeroPerk data;

    class HeroPerkBaker : Unity.Entities.Baker<HeroPerkAuthoring>
    {
        public override void Bake(HeroPerkAuthoring authoring)
        {
            if (authoring.data == null)
                return;

            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new HeroPerkComponent { perkID = authoring.data.perkID });
        }
    }
}
