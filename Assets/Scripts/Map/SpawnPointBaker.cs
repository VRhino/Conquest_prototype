using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class SpawnPointBaker : Baker<SpawnPointAuthoring>
{
    public override void Bake(SpawnPointAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity, new SpawnPointComponent
        {
            spawnID = authoring.spawnID,
            teamID = authoring.teamID,
            isActive = authoring.isActive,
            position = authoring.transform.position
        });
    }
}
