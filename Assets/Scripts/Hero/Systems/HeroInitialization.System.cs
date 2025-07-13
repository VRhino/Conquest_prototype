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
                fuerza = def.baseFuerza,
                destreza = def.baseDestreza,
                armadura = def.baseArmadura,
                vitalidad = def.baseVitalidad,
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
            // Fórmulas: vidaMax = 100 + (5 * vitalidad), estaminaMax = 100 + (3 * destreza / 2)
            float vidaMax = 100f + (5f * attributes.vitalidad);
            float estaminaMax = 100f + (3f * attributes.destreza / 2f);
            
            var health = healthLookup[entity];
            health.maxHealth = vidaMax;
            health.currentHealth = vidaMax;
            healthLookup[entity] = health;

            var stamina = staminaLookup[entity];
            stamina.maxStamina = estaminaMax;
            stamina.currentStamina = estaminaMax;
            staminaLookup[entity] = stamina;
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
