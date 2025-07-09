using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// MonoBehaviour used to bake <see cref="HeroClassDefinition"/> assets into
/// ECS components and buffers that can be consumed at runtime.
/// </summary>
public class HeroClassDefinitionAuthoring : MonoBehaviour
{
    public HeroClassDefinition definition;

    class HeroClassDefinitionBaker : Unity.Entities.Baker<HeroClassDefinitionAuthoring>
    {
        public override void Bake(HeroClassDefinitionAuthoring authoring)
        {
            if (authoring.definition == null)
                return;

            var entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new HeroClassComponent
            {
                heroClass = authoring.definition.heroClass
            });

            var abilities = AddBuffer<HeroAbilityBufferElement>(entity);
            if (authoring.definition.abilities != null)
            {
                foreach (var ability in authoring.definition.abilities)
                {
                    if (ability == null)
                        continue;

                    abilities.Add(new HeroAbilityBufferElement
                    {
                        name = ability.abilityName,
                        cooldown = ability.cooldown,
                        staminaCost = ability.staminaCost,
                        damageMultiplier = ability.damageMultiplier,
                        category = (AbilityCategory)ability.category
                    });
                }
            }
        }
    }
}
