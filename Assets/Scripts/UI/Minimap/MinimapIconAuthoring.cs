using Unity.Entities;
using UnityEngine;

/// <summary>
/// Authoring component used to attach a <see cref="MinimapIconComponent"/>
/// to an entity at bake time.
/// </summary>
public class MinimapIconAuthoring : MonoBehaviour
{
    public MinimapIconType iconType = MinimapIconType.Hero;
    public Team teamAffiliation = Team.None;

    class Baker : Unity.Entities.Baker<MinimapIconAuthoring>
    {
        public override void Bake(MinimapIconAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new MinimapIconComponent
            {
                iconType = authoring.iconType,
                teamAffiliation = authoring.teamAffiliation,
                worldPosition = authoring.transform.position
            });
        }
    }
}
