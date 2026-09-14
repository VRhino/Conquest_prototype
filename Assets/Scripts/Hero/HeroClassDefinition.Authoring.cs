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
            var definition = authoring.definition;
            DependsOn(definition);
            var classData = new HeroClassDefinitionComponent
            {
                heroClass = definition.heroClass,
                baseStrength = definition.baseStrength, baseDexterity = definition.baseDexterity,
                baseArmor = definition.baseArmor, baseVitality = definition.baseVitality,
                minStrength = definition.minStrength, maxStrength = Mathf.Max(definition.minStrength, definition.maxStrength),
                minDexterity = definition.minDexterity, maxDexterity = Mathf.Max(definition.minDexterity, definition.maxDexterity),
                minArmor = definition.minArmor, maxArmor = Mathf.Max(definition.minArmor, definition.maxArmor),
                minVitality = definition.minVitality, maxVitality = Mathf.Max(definition.minVitality, definition.maxVitality)
            };

            AddComponent(entity, new HeroClassComponent
            {
                heroClass = authoring.definition.heroClass
            });

            var bakedAbilities = new System.Collections.Generic.List<HeroAbilityBufferElement>();
            if (authoring.definition.abilities != null)
            {
                foreach (var ability in authoring.definition.abilities)
                {
                    if (ability == null)
                        continue;

                    DependsOn(ability);
                    var abilityEntity = CreateAdditionalEntity(TransformUsageFlags.None);
                    var abilityData = new HeroAbilityBufferElement
                    {
                        name = ability.abilityName,
                        cooldown = ability.cooldown,
                        staminaCost = ability.staminaCost,
                        damageMultiplier = ability.damageMultiplier,
                        category = (AbilityCategory)ability.category
                    };
                    AddComponent(abilityEntity, new HeroAbilityDefinitionComponent { data = abilityData });
                    bakedAbilities.Add(abilityData);
                    switch (ability.category)
                    {
                        case HeroAbilityCategory.Q: classData.abilityQ = abilityEntity; break;
                        case HeroAbilityCategory.E: classData.abilityE = abilityEntity; break;
                        case HeroAbilityCategory.R: classData.abilityR = abilityEntity; break;
                        case HeroAbilityCategory.Ultimate: classData.ultimate = abilityEntity; break;
                    }
                }
            }
            var bakedPerks = new System.Collections.Generic.List<ValidPerkElement>();
            if (definition.validClassPerks != null)
                foreach (var perk in definition.validClassPerks)
                {
                    if (perk == null) continue;
                    DependsOn(perk);
                    var perkEntity = CreateAdditionalEntity(TransformUsageFlags.None);
                    AddComponent(perkEntity, new HeroPerkComponent { perkID = perk.perkID });
                    bakedPerks.Add(new ValidPerkElement { Value = perkEntity });
                }
            AddComponent(entity, classData);
            var abilities = AddBuffer<HeroAbilityBufferElement>(entity);
            foreach (var ability in bakedAbilities) abilities.Add(ability);
            var perks = AddBuffer<ValidPerkElement>(entity);
            foreach (var perk in bakedPerks) perks.Add(perk);
        }
    }
}
