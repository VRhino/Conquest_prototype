using Unity.Entities;
using UnityEngine;

/// <summary>
/// MonoBehaviour used on the hero prefab to specify which
/// <see cref="HeroClassDefinition"/> is assigned to that hero.
/// </summary>
public class HeroClassReferenceAuthoring : MonoBehaviour
{
    public HeroClassDefinitionAuthoring classDefinition; // Cambiado

    class HeroClassReferenceBaker : Baker<HeroClassReferenceAuthoring>
    {
        public override void Bake(HeroClassReferenceAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            if (authoring.classDefinition == null)
                return;

            var defEntity = GetEntity(authoring.classDefinition, TransformUsageFlags.None);
            AddComponent(entity, new HeroClassReference { classEntity = defEntity });
        }
    }
}

/// <summary>
/// Component storing a reference to the hero class definition entity.
/// </summary>
public struct HeroClassReference : IComponentData
{
    public Entity classEntity;
}