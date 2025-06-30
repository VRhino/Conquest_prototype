using Unity.Entities;

/// <summary>
/// Baker for HeroStatsAuthoring. Adds HeroStatsComponent to the entity during baking.
/// </summary>
public class HeroStatsBaker : Baker<HeroStatsAuthoring>
{
    public override void Bake(HeroStatsAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent(entity, new HeroStatsComponent
        {
            baseSpeed = authoring.baseSpeed,
            sprintMultiplier = authoring.sprintMultiplier,
            jumpForce = authoring.jumpForce
        });
    }
}
