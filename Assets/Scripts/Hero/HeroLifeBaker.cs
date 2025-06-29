using Unity.Entities;

public class HeroLifeBaker : Baker<HeroLifeAuthoring>
{
    public override void Bake(HeroLifeAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity, new HeroLifeComponent
        {
            isAlive = authoring.isAlive,
            deathTimer = authoring.deathTimer,
            respawnCooldown = authoring.respawnCooldown
        });
    }
}
