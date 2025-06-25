using Unity.Entities;
using UnityEngine;

/// <summary>
/// MonoBehaviour used to bake a <see cref="DamageProfile"/> into a component.
/// </summary>
public class DamageProfileAuthoring : MonoBehaviour
{
    public DamageProfile profile;

    class Baker : Unity.Entities.Baker<DamageProfileAuthoring>
    {
        public override void Bake(DamageProfileAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            if (authoring.profile == null)
                return;

            AddComponent(entity, new DamageProfileComponent
            {
                baseDamage = authoring.profile.baseDamage,
                damageType = authoring.profile.damageType,
                penetration = authoring.profile.penetration
            });
        }
    }
}
