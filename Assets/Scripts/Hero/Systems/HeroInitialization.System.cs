using Unity.Collections;
using Unity.Entities;
using UnityEngine;
/// <summary>
/// Initializes hero attributes, abilities and perk lists when a hero is
/// created or loaded from saved progression, and puts its stats on the modifier stack
/// (<see cref="StatResolveSystem"/> writes health, stamina, defence and damage from it).
/// </summary>
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class HeroInitializationSystem : SystemBase
{
    /// <summary>Heroes without a class to seed from, so they are not retried (and logged) every frame.</summary>
    readonly System.Collections.Generic.HashSet<Entity> _unseedable = new();

    protected override void OnUpdate()
    {

        var defLookup = GetComponentLookup<HeroClassDefinitionComponent>(true);
        var perkLookup = GetBufferLookup<ValidPerkElement>(true);

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

        ecb.Playback(EntityManager);
        ecb.Dispose();

        // Heroes whose stats are not on the stack yet (after the playback, so new heroes already have their attributes).
        // Seeding makes structural changes, so it runs after the query.
        var pending = new NativeList<Entity>(Allocator.Temp);
        foreach (var (_, entity) in SystemAPI.Query<RefRO<HeroHealthComponent>>()
                     .WithAll<StaminaComponent>()
                     .WithNone<HeroStatFormulaComponent>()
                     .WithEntityAccess())
            if (!_unseedable.Contains(entity)) pending.Add(entity);

        foreach (var entity in pending)
            SeedStats(entity);
        pending.Dispose();
    }

    /// <summary>
    /// Puts the hero's stats on the modifier stack (T1, L7-3): attributes as bases, equipment as flat modifiers on them,
    /// and the class formula, which <see cref="StatResolveSystem"/> applies to write health, stamina, defence and
    /// damage. The local hero takes its attributes and equipment from its save; remote and AI heroes take the class
    /// attributes and the equipment they were spawned with.
    /// </summary>
    void SeedStats(Entity entity)
    {
        HeroBaseStats baseStats;
        EquipmentBonuses equipment;
        HeroClassDefinition classData;
        var selectedHero = PlayerSessionService.SelectedHero;
        if (EntityManager.HasComponent<IsLocalPlayer>(entity) && selectedHero != null)
        {
            classData = HeroClassManager.GetClassDefinition(selectedHero.classId);
            baseStats = HeroBaseStats.FromHeroData(selectedHero);
            equipment = DataCacheService.GetEquipmentBonuses(selectedHero);
        }
        else
        {
            classData = HeroClassManager.GetClassDefinition(ClassIdOf(entity));
            baseStats = ClassAttributesOf(entity, classData);
            equipment = EntityManager.HasComponent<HeroAppearanceComponent>(entity)
                ? DataCacheService.GetEquipmentBonuses(EntityManager.GetComponentObject<HeroAppearanceComponent>(entity).equipment)
                : EquipmentBonuses.Empty;
        }

        if (classData == null)
        {
            // Keeps the health and stamina it was spawned with; logged once, not every frame.
            _unseedable.Add(entity);
            Debug.LogWarning($"[HeroInitializationSystem] Sin clase para {entity}: sus stats no pasan por la pila.");
            return;
        }

        EntityManager.AddComponentData(entity, HeroStatFormula.From(classData));
        if (!EntityManager.HasComponent<DefenseComponent>(entity)) EntityManager.AddComponent<DefenseComponent>(entity);
        if (!EntityManager.HasComponent<DamageProfileComponent>(entity)) EntityManager.AddComponent<DamageProfileComponent>(entity);

        StatStack.Ensure(EntityManager, entity);
        StatStack.SetBase(EntityManager, entity, StatId.Strength, baseStats.baseStrength);
        StatStack.SetBase(EntityManager, entity, StatId.Dexterity, baseStats.baseDexterity);
        StatStack.SetBase(EntityManager, entity, StatId.Armor, baseStats.baseArmor);
        StatStack.SetBase(EntityManager, entity, StatId.Vitality, baseStats.baseVitality);
        if (EntityManager.HasComponent<HeroStatsComponent>(entity))
        {
            float baseSpeed = EntityManager.GetComponentData<HeroStatsComponent>(entity).baseSpeed;
            StatStack.SetBase(EntityManager, entity, StatId.MoveSpeed, baseSpeed);
            EntityManager.AddComponentData(entity, new MoveSpeedComponent { value = baseSpeed });
        }
        StatStack.ClearSource(EntityManager, entity, ModifierSource.Equipment);
        AddEquipment(entity, StatId.Strength, equipment.strengthBonus);
        AddEquipment(entity, StatId.Dexterity, equipment.dexterityBonus);
        AddEquipment(entity, StatId.Armor, equipment.armorBonus);
        AddEquipment(entity, StatId.Vitality, equipment.vitalityBonus);
        StatStack.MarkDirty(EntityManager, entity);
    }

    /// <summary>
    /// Class of a hero that is not the local one: the class set at spawn (remote and AI heroes), or the class definition
    /// entity it was baked with.
    /// </summary>
    string ClassIdOf(Entity entity)
    {
        if (EntityManager.HasComponent<HeroClassComponent>(entity))
            return EntityManager.GetComponentData<HeroClassComponent>(entity).heroClass.ToString();
        if (EntityManager.HasComponent<HeroAttributesComponent>(entity))
        {
            var classEntity = EntityManager.GetComponentData<HeroAttributesComponent>(entity).classDefinition;
            if (EntityManager.HasComponent<HeroClassDefinitionComponent>(classEntity))
                return EntityManager.GetComponentData<HeroClassDefinitionComponent>(classEntity).heroClass.ToString();
        }
        return null;
    }

    /// <summary>The hero's attributes when it has them (validated against its class), else its class's base ones.</summary>
    HeroBaseStats ClassAttributesOf(Entity entity, HeroClassDefinition classData)
    {
        if (EntityManager.HasComponent<HeroAttributesComponent>(entity))
        {
            var a = EntityManager.GetComponentData<HeroAttributesComponent>(entity);
            return new HeroBaseStats { baseStrength = a.strength, baseDexterity = a.dexterity, baseArmor = a.armor, baseVitality = a.vitality };
        }
        if (classData == null) return default;
        return new HeroBaseStats
        {
            baseStrength = classData.baseStrength,
            baseDexterity = classData.baseDexterity,
            baseArmor = classData.baseArmor,
            baseVitality = classData.baseVitality
        };
    }

    void AddEquipment(Entity entity, StatId attribute, int bonus)
    {
        if (bonus != 0) StatStack.Add(EntityManager, entity, attribute, ModifierOp.Flat, bonus, ModifierSource.Equipment);
    }
}
