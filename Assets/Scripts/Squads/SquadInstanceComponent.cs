using Unity.Entities;

/// <summary>
/// Identifies a specific squad instance owned by the player.
/// Used to map runtime entities to saved progress.
/// </summary>
public struct SquadInstanceComponent : IComponentData
{
    /// <summary>Unique identifier of the squad instance.</summary>
    public int id;
}

/// <summary>
/// Authoring behaviour used to assign a squad instance ID on prefabs.
/// </summary>
public class SquadInstanceAuthoring : UnityEngine.MonoBehaviour
{
    public int id = 0;

    class Baker : Unity.Entities.Baker<SquadInstanceAuthoring>
    {
        public override void Bake(SquadInstanceAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new SquadInstanceComponent { id = authoring.id });
        }
    }
}
