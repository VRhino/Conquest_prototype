using Unity.Entities;
using Unity.Mathematics;

public class HeroSpawnBaker : Baker<HeroSpawnAuthoring>
{
    public override void Bake(HeroSpawnAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity, new HeroSpawnComponent
        {
            spawnId = authoring.spawnId,
            spawnPosition = authoring.spawnPosition,
            spawnRotation = authoring.spawnRotation,
            hasSpawned = authoring.hasSpawned
        });
    }
}
