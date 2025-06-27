using Unity.Entities;

/// <summary>
/// Baker that converts <see cref="PerkData"/> assets into entities with
/// <see cref="PerkDataComponent"/> so they can be referenced at runtime.
/// </summary>
public class PerkDataBaker : Unity.Entities.Baker<PerkData>
{
    public override void Bake(PerkData authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity, new PerkDataComponent { perkID = authoring.perkID });
    }
}
