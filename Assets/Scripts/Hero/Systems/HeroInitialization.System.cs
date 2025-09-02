using Unity.Collections;
using Unity.Entities;
using UnityEngine;
/// <summary>
/// Initializes hero attributes, abilities and perk lists when a hero is
/// created or loaded from saved progression.
/// </summary>
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class HeroInitializationSystem : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<HeroClassReference>();
    }

    protected override void OnUpdate()
    {
        var defLookup = GetComponentLookup<HeroClassDefinitionComponent>(true);
        var perkLookup = GetBufferLookup<ValidPerkElement>(true);
        var staminaLookup = SystemAPI.GetComponentLookup<StaminaComponent>(false);
        var healthLookup = SystemAPI.GetComponentLookup<HeroHealthComponent>(false);
        var attrLookup = SystemAPI.GetComponentLookup<HeroAttributesComponent>(true);

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (classRef, entity) in SystemAPI
                     .Query<RefRO<HeroClassReference>>()
                     .WithNone<HeroAttributesComponent>()
                     .WithEntityAccess())
        {
            if (!defLookup.TryGetComponent(classRef.ValueRO.classEntity, out var def))
                continue;

            ecb.AddComponent(entity, new HeroAttributesComponent
            {
                strength = def.baseStrength,
                dexterity = def.baseDexterity,
                armor = def.baseArmor,
                vitality = def.baseVitality,
                classDefinition = classRef.ValueRO.classEntity
            });

            ecb.AddComponent(entity, new HeroAbilityComponent
            {
                abilityQ = def.abilityQ,
                abilityE = def.abilityE,
                abilityR = def.abilityR,
                ultimate = def.ultimate
            });

            if (perkLookup.HasBuffer(classRef.ValueRO.classEntity))
            {
                var source = perkLookup[classRef.ValueRO.classEntity];
                var dest = ecb.AddBuffer<ValidPerkElement>(entity);
                foreach (var perk in source)
                    dest.Add(perk);
            }
        }

        foreach (var (attr, entity) in SystemAPI.Query<RefRO<HeroAttributesComponent>>().WithEntityAccess())
        {
            // Solo calcular si ambos componentes existen
            if (!healthLookup.HasComponent(entity) || !staminaLookup.HasComponent(entity))
                continue;

            var attributes = attr.ValueRO;
            
            // Obtener la definición de clase desde el componente
            HeroClassDefinition classData = null;
            if (defLookup.HasComponent(entity))
            {
                var classDef = defLookup[entity];
                // Convertir enum a string para buscar la definición
                string classId = classDef.heroClass.ToString();
                classData = HeroClassManager.GetClassDefinition(classId);
                if(classData == null) Debug.LogError($"[HeroInitializationSystem] No se encontró HeroClassDefinition para classId: {classId}");
            }

            CalculatedAttributes calculatedAttributes =
                DataCacheService.CalculateDerivedAttributes(
                    classData,
                    attributes.strength,
                    attributes.dexterity,
                    attributes.armor,
                    attributes.vitality,
                    0);

            var health = healthLookup[entity];
            health.maxHealth = calculatedAttributes.maxHealth;
            health.currentHealth = calculatedAttributes.maxHealth;
            healthLookup[entity] = health;

            var stamina = staminaLookup[entity];
            stamina.maxStamina = calculatedAttributes.stamina;
            stamina.currentStamina = calculatedAttributes.stamina;
            staminaLookup[entity] = stamina;
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
