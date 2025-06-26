using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Baker that converts <see cref="HeroClassDefinition"/> assets into entities
/// with <see cref="HeroClassDefinitionComponent"/> and a buffer of
/// <see cref="ValidPerkElement"/> references.
/// </summary>
public class HeroClassDefinitionBaker : Baker<HeroClassDefinition>
{
    public override void Bake(HeroClassDefinition authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);

        AddComponent(entity, new HeroClassDefinitionComponent
        {
            heroClass = authoring.heroClass,
            baseFuerza = authoring.baseFuerza,
            baseDestreza = authoring.baseDestreza,
            baseArmadura = authoring.baseArmadura,
            baseVitalidad = authoring.baseVitalidad,
            minFuerza = authoring.minFuerza,
            maxFuerza = authoring.maxFuerza,
            minDestreza = authoring.minDestreza,
            maxDestreza = authoring.maxDestreza,
            minArmadura = authoring.minArmadura,
            maxArmadura = authoring.maxArmadura,
            minVitalidad = authoring.minVitalidad,
            maxVitalidad = authoring.maxVitalidad,
            abilityQ = authoring.abilities.Count > 0 ? GetEntity(authoring.abilities[0], TransformUsageFlags.None) : Entity.Null,
            abilityE = authoring.abilities.Count > 1 ? GetEntity(authoring.abilities[1], TransformUsageFlags.None) : Entity.Null,
            abilityR = authoring.abilities.Count > 2 ? GetEntity(authoring.abilities[2], TransformUsageFlags.None) : Entity.Null,
            ultimate = authoring.abilities.Count > 3 ? GetEntity(authoring.abilities[3], TransformUsageFlags.None) : Entity.Null
        });

        var buffer = AddBuffer<ValidPerkElement>(entity);
        if (authoring.validClassPerks != null)
        {
            foreach (var perk in authoring.validClassPerks)
            {
                if (perk != null)
                    buffer.Add(new ValidPerkElement { Value = GetEntity(perk, TransformUsageFlags.None) });
            }
        }
    }
}
