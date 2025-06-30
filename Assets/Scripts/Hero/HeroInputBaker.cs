using Unity.Entities;

/// <summary>
/// Baker for HeroInputAuthoring. Adds HeroInputComponent to the entity during baking.
/// </summary>
public class HeroInputBaker : Baker<HeroInputAuthoring>
{
    public override void Bake(HeroInputAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent<HeroInputComponent>(entity);
    }
}
