using Unity.Entities;
using UnityEngine;

/// <summary>
/// MonoBehaviour used to bake <see cref="PerkData"/> assets into entities
/// with <see cref="PerkDataComponent"/> so they can be referenced at runtime.
/// </summary>
public class PerkDataAuthoring : MonoBehaviour
{
    public PerkData data;

    class PerkDataBaker : Unity.Entities.Baker<PerkDataAuthoring>
    {
        public override void Bake(PerkDataAuthoring authoring)
        {
            if (authoring.data == null)
                return;

            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new PerkDataComponent { perkID = authoring.data.perkID });
        }
    }
}
