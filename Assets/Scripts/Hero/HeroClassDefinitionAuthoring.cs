using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// MonoBehaviour used to bake <see cref="HeroClassDefinition"/> assets into entities
/// with <see cref="HeroClassDefinitionComponent"/> and a buffer of
/// <see cref="ValidPerkElement"/> references.
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

            AddComponent(entity, new HeroClassDefinitionComponent
            {
                heroClass = authoring.definition.heroClass,
                baseFuerza = authoring.definition.baseFuerza,
                baseDestreza = authoring.definition.baseDestreza,
                baseArmadura = authoring.definition.baseArmadura,
                baseVitalidad = authoring.definition.baseVitalidad,
                minFuerza = authoring.definition.minFuerza,
                maxFuerza = authoring.definition.maxFuerza,
                minDestreza = authoring.definition.minDestreza,
                maxDestreza = authoring.definition.maxDestreza,
                minArmadura = authoring.definition.minArmadura,
                maxArmadura = authoring.definition.maxArmadura,
                minVitalidad = authoring.definition.minVitalidad,
                maxVitalidad = authoring.definition.maxVitalidad,
                abilityQ = authoring.definition.abilities.Count > 0 ? GetEntity(authoring.definition.abilities[0], TransformUsageFlags.None) : Entity.Null,
                abilityE = authoring.definition.abilities.Count > 1 ? GetEntity(authoring.definition.abilities[1], TransformUsageFlags.None) : Entity.Null,
                abilityR = authoring.definition.abilities.Count > 2 ? GetEntity(authoring.definition.abilities[2], TransformUsageFlags.None) : Entity.Null,
                ultimate = authoring.definition.abilities.Count > 3 ? GetEntity(authoring.definition.abilities[3], TransformUsageFlags.None) : Entity.Null
            });

            var buffer = AddBuffer<ValidPerkElement>(entity);
            if (authoring.definition.validClassPerks != null)
            {
                foreach (var perk in authoring.definition.validClassPerks)
                {
                    if (perk != null)
                        buffer.Add(new ValidPerkElement { Value = GetEntity(perk, TransformUsageFlags.None) });
                }
            }
        }
    }
}
