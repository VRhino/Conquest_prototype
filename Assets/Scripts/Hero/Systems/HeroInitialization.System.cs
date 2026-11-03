using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
/// <summary>
/// Puts each new hero's stats, skills and perks on the modifier stack from its class asset (<see cref="HeroClassManager"/>
/// is the only source of class data; <see cref="StatResolveSystem"/> writes health, stamina, defence and damage from it).
/// </summary>
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class HeroInitializationSystem : SystemBase
{
    /// <summary>Heroes without a class to seed from, so they are not retried (and logged) every frame.</summary>
    readonly System.Collections.Generic.HashSet<Entity> _unseedable = new();

    /// <summary>Skill blobs shared by the heroes of this world (L2-2).</summary>
    readonly SkillLibrary _skills = new();

    protected override void OnDestroy() => _skills.Dispose();

    protected override void OnUpdate()
    {
        // Heroes whose stats are not on the stack yet. Seeding makes structural changes, so it runs after the query.
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
            baseStats = ClassAttributesOf(classData);
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
        // The class weapon's hit traits; its damage and penetration come from the stack.
        var heroProfile = EntityManager.GetComponentData<DamageProfileComponent>(entity);
        heroProfile.traits = classData.hitTraits;
        EntityManager.SetComponentData(entity, heroProfile);

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
        if (EntityManager.HasComponent<HeroCombatComponent>(entity))
        {
            var combat = EntityManager.GetComponentData<HeroCombatComponent>(entity);
            StatStack.SeedCritical(EntityManager, entity, combat.criticalChance, combat.criticalMultiplier);
        }
        StatStack.ClearSource(EntityManager, entity, ModifierSource.Equipment);
        AddEquipment(entity, StatId.Strength, equipment.strengthBonus);
        AddEquipment(entity, StatId.Dexterity, equipment.dexterityBonus);
        AddEquipment(entity, StatId.Armor, equipment.armorBonus);
        AddEquipment(entity, StatId.Vitality, equipment.vitalityBonus);
        StatStack.MarkDirty(EntityManager, entity);
        SeedSkills(entity, classData);
        if (EntityManager.HasComponent<IsLocalPlayer>(entity) && PlayerSessionService.SelectedHero != null)
            SeedPerks(entity, classData, PerkLoadout.ChosenIds(PlayerSessionService.SelectedHero));
    }

    readonly System.Collections.Generic.List<HeroPerk> _passives = new();
    readonly System.Collections.Generic.List<HeroPerk> _actives = new();

    /// <summary>
    /// The perks of the hero's loadout (L7-4): each effect of the chosen passive perks goes to its
    /// <see cref="HeroPerkElement"/> buffer, applied by the perk systems; the two active ones become skills on keys 1 and 2.
    /// Remote and AI heroes have no loadout perks.
    /// </summary>
    void SeedPerks(Entity entity, HeroClassDefinition classData, System.Collections.Generic.IEnumerable<int> chosenIds)
    {
        PerkLoadout.Select(classData.validClassPerks, chosenIds, _passives, _actives);
        if (EntityManager.HasBuffer<HeroSkillElement>(entity))
            for (int i = 0; i < _actives.Count; i++)
            {
                var slot = i == 0 ? SkillSlot.ActivePerk1 : SkillSlot.ActivePerk2;
                var blob = _skills.GetPerk(_actives[i], slot);
                EntityManager.GetBuffer<HeroSkillElement>(entity).Add(new HeroSkillElement { slot = slot, skillId = blob.Value.id, skill = blob });
            }
        if (_passives.Count == 0) return;
        var perks = EntityManager.HasBuffer<HeroPerkElement>(entity)
            ? EntityManager.GetBuffer<HeroPerkElement>(entity)
            : EntityManager.AddBuffer<HeroPerkElement>(entity);
        perks.Clear();
        foreach (var perk in _passives)
            if (perk.effects != null)
                foreach (var effect in perk.effects)
                    perks.Add(new HeroPerkElement { perkId = perk.perkID, effect = effect });
        if (!EntityManager.HasComponent<HeroMotionComponent>(entity)) EntityManager.AddComponent<HeroMotionComponent>(entity);
    }

    /// <summary>
    /// The skills the hero can launch (L2-1, L2-2): the basic attack from its weapon timings and the four class abilities
    /// from the class asset, not from the baked class entity (empty in battle).
    /// </summary>
    void SeedSkills(Entity entity, HeroClassDefinition classData)
    {
        if (!EntityManager.HasBuffer<CooldownGroupElement>(entity)) EntityManager.AddBuffer<CooldownGroupElement>(entity);
        if (!EntityManager.HasComponent<SkillInstanceComponent>(entity)) EntityManager.AddComponent<SkillInstanceComponent>(entity);
        if (!EntityManager.HasComponent<ParkedSkillComponent>(entity)) EntityManager.AddComponent<ParkedSkillComponent>(entity);
        if (!EntityManager.HasComponent<EndSkillsRequestComponent>(entity))
        {
            EntityManager.AddComponent<EndSkillsRequestComponent>(entity);
            EntityManager.SetComponentEnabled<EndSkillsRequestComponent>(entity, false);
        }
        if (!EntityManager.HasComponent<SkillRequestComponent>(entity))
        {
            EntityManager.AddComponent<SkillRequestComponent>(entity);
            EntityManager.SetComponentEnabled<SkillRequestComponent>(entity, false);
        }
        if (!EntityManager.HasComponent<CooldownScaleComponent>(entity))
            EntityManager.AddComponentData(entity, new CooldownScaleComponent { value = 1f });
        StatStack.SetBase(EntityManager, entity, StatId.CooldownScale, 1f);

        var skills = EntityManager.HasBuffer<HeroSkillElement>(entity)
            ? EntityManager.GetBuffer<HeroSkillElement>(entity)
            : EntityManager.AddBuffer<HeroSkillElement>(entity);
        skills.Clear();
        if (EntityManager.HasComponent<HeroCombatComponent>(entity)
            && SystemAPI.TryGetSingleton<HeroGameplayConfigComponent>(out var config))
        {
            var combat = EntityManager.GetComponentData<HeroCombatComponent>(entity);
            int hits = math.max(1, config.basicComboLength);
            for (int hit = 0; hit < hits; hit++)
            {
                var basic = _skills.GetBasicAttack(combat, config.attackStaminaCost, config.attackCooldown, hit, hits, config.comboChainWindow);
                skills.Add(new HeroSkillElement { slot = SkillSlot.Basic, skillId = basic.Value.id, skill = basic });
            }
        }
        if (classData.abilities == null) return;
        foreach (var ability in classData.abilities)
        {
            if (ability == null) continue;
            var blob = _skills.Get(ability);
            skills.Add(new HeroSkillElement { slot = SkillBlobBuilder.SlotOf(ability.category), skillId = blob.Value.id, skill = blob });
        }
    }

    /// <summary>
    /// Class of a hero that is not the local one: the class set at spawn (remote and AI heroes).
    /// </summary>
    string ClassIdOf(Entity entity)
    {
        return EntityManager.HasComponent<HeroClassComponent>(entity)
            ? EntityManager.GetComponentData<HeroClassComponent>(entity).heroClass.ToString()
            : null;
    }

    /// <summary>The base attributes of the hero's class (remote and AI heroes have no save to take them from).</summary>
    static HeroBaseStats ClassAttributesOf(HeroClassDefinition classData)
    {
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
