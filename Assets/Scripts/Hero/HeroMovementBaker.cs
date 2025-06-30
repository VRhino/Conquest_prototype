using Unity.Entities;

/// <summary>
/// Baker for <see cref="HeroMovementAuthoring"/>. Adds <see cref="HeroMovementComponent"/> during baking.
/// </summary>
public class HeroMovementBaker : Baker<HeroMovementAuthoring>
{
    public override void Bake(HeroMovementAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent(entity, new HeroMovementComponent
        {
            movementSpeed = authoring.movementSpeed
        });
    }
}
