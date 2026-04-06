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
        Debug.Log("[BattleTestDebug] HeroInitializationSystem.OnUpdate running");
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

        foreach (var (attr, entity) in SystemAPI.Query<RefRO<HeroAttributesComponent>>().WithNone<DefenseComponent>().WithEntityAccess())
        {
            // Solo calcular si ambos componentes existen
            if (!healthLookup.HasComponent(entity) || !staminaLookup.HasComponent(entity))
                continue;

            var attributes = attr.ValueRO;

            // Para el héroe local, la cache es la fuente de verdad al entrar en batalla.
            // No hay cambios de equipamiento ni nivel durante la batalla, así que la cache
            // es estable y correcta. Solo se calcula una vez (guard WithNone<DefenseComponent>).
            HeroCalculatedAttributes calculatedAttributes;
            bool usingCache = false;

            if (SystemAPI.HasComponent<IsLocalPlayer>(entity) && PlayerSessionService.SelectedHero != null)
            {
                var selectedHero = PlayerSessionService.SelectedHero;
                string cacheKey = string.IsNullOrEmpty(selectedHero.heroName) ? selectedHero.classId : selectedHero.heroName;
                var cached = DataCacheService.GetHeroCalculatedAttributes(cacheKey);
                if (cached.maxHealth > 0f)
                {
                    calculatedAttributes = cached;
                    usingCache = true;
                }
                else
                {
                    calculatedAttributes = HeroCalculatedAttributes.Empty;
                    Debug.LogWarning("[HeroInitializationSystem] Cache vacía para héroe local, usando fallback de clase.");
                }
            }
            else
            {
                calculatedAttributes = HeroCalculatedAttributes.Empty;
            }

            if (!usingCache)
            {
                // Fallback para héroes remotos o cache vacía: calcular desde definición de clase
                HeroClassDefinition classData = null;
                if (defLookup.HasComponent(attributes.classDefinition))
                {
                    var classDef = defLookup[attributes.classDefinition];
                    string classId = classDef.heroClass.ToString();
                    classData = HeroClassManager.GetClassDefinition(classId);
                    if (classData == null) Debug.LogError($"[HeroInitializationSystem] No se encontró HeroClassDefinition para classId: {classId}");
                }

                var baseStats = new HeroBaseStats
                {
                    baseStrength = attributes.strength,
                    baseDexterity = attributes.dexterity,
                    baseArmor = attributes.armor,
                    baseVitality = attributes.vitality
                };

                calculatedAttributes = HeroCalculatedAttributes.Calculate(baseStats, DataCacheService.GetEquipmentBonuses(), EquipmentBonuses.Empty, classData);
            }

            var health = healthLookup[entity];
            health.maxHealth = calculatedAttributes.maxHealth;
            health.currentHealth = calculatedAttributes.maxHealth;
            healthLookup[entity] = health;
            Debug.Log($"[BattleTestDebug] HeroInitialization: maxHealth={calculatedAttributes.maxHealth}, usingCache={usingCache}, entity={entity}");

            var stamina = staminaLookup[entity];
            stamina.maxStamina = calculatedAttributes.stamina;
            stamina.currentStamina = calculatedAttributes.stamina;
            staminaLookup[entity] = stamina;

            if (!SystemAPI.HasComponent<DefenseComponent>(entity))
            {
                ecb.AddComponent(entity, new DefenseComponent
                {
                    bluntDefense  = calculatedAttributes.bluntDefense,
                    slashDefense  = calculatedAttributes.slashDefense,
                    pierceDefense = calculatedAttributes.pierceDefense
                });
            }

            if (!SystemAPI.HasComponent<DamageProfileComponent>(entity))
            {
                ecb.AddComponent(entity, new DamageProfileComponent
                {
                    bluntDamage    = calculatedAttributes.bluntDamage,
                    slashingDamage = calculatedAttributes.slashingDamage,
                    piercingDamage = calculatedAttributes.piercingDamage
                });
            }
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
