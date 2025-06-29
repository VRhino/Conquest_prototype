using Unity.Entities;
using UnityEngine;

public class HeroPrefabBaker : Baker<HeroPrefabAuthoring>
{
    public override void Bake(HeroPrefabAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity, new HeroPrefabComponent
        {
            prefab = GetEntity(authoring.heroPrefab, TransformUsageFlags.Dynamic)
        });
    }
}
