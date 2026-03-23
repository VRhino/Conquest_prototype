using Unity.Entities;
using UnityEngine;

/// <summary>
/// MonoBehaviour used to bake a <see cref="DamageProfile"/> into a component.
/// </summary>
public class DamageProfileAuthoring : MonoBehaviour
{
    public DamageProfile profile;

    class DamageProfileBaker : Unity.Entities.Baker<DamageProfileAuthoring>
    {
        public override void Bake(DamageProfileAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            if (authoring.profile == null)
                return;

            var t = authoring.profile.damageType;
            float d = authoring.profile.baseDamage;
            float p = authoring.profile.penetration;
            AddComponent(entity, new DamageProfileComponent
            {
                bluntDamage         = t == DamageType.Blunt    ? d : 0f,
                slashingDamage      = t == DamageType.Slashing ? d : 0f,
                piercingDamage      = t == DamageType.Piercing ? d : 0f,
                bluntPenetration    = t == DamageType.Blunt    ? p : 0f,
                slashingPenetration = t == DamageType.Slashing ? p : 0f,
                piercingPenetration = t == DamageType.Piercing ? p : 0f,
            });
        }
    }
}
