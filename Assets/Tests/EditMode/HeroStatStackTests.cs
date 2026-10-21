using System.Collections.Generic;
using NUnit.Framework;
using Unity.Entities;

/// <summary>
/// Heroes and equipment through the modifier stack (Aprendizajes_Referencia.md T1, L7-3): the resolver gives the same
/// numbers as <see cref="HeroCalculatedAttributes.Calculate"/>, and remote (AI) heroes now count their equipment.
/// </summary>
public class HeroStatStackTests
{
    const string ClassId = nameof(HeroClass.SwordAndShield);

    [Test]
    public void RemoteHeroWithEquipmentResolvesLikeTheClassFormula()
    {
        var classData = HeroClassManager.GetClassDefinition(ClassId);
        Assert.That(classData, Is.Not.Null, $"Resources/Data/HeroClasses/{ClassId}");

        using var world = new World("Hero stats");
        var em = world.EntityManager;
        var classEntity = em.CreateEntity();
        em.AddComponentData(classEntity, new HeroClassDefinitionComponent
        {
            heroClass = HeroClass.SwordAndShield,
            baseStrength = 12, baseDexterity = 9, baseArmor = 7, baseVitality = 11,
        });
        var hero = em.CreateEntity();
        em.AddComponentData(hero, new HeroClassReference { classEntity = classEntity });
        em.AddComponentData(hero, new HeroHealthComponent { maxHealth = 100f, currentHealth = 100f });
        em.AddComponentData(hero, new StaminaComponent { maxStamina = 100f, currentStamina = 100f });
        var armour = new InventoryItem { itemId = "test_torso", instanceId = "test-1" };
        armour.GeneratedStats = new Dictionary<string, float> { ["Vitality"] = 5f, ["Armor"] = 3f };
        em.AddComponentObject(hero, new HeroAppearanceComponent { equipment = new Equipment { torso = armour } });

        var init = world.GetOrCreateSystemManaged<HeroInitializationSystem>();
        init.Update(); // adds the attributes
        init.Update(); // seeds the stack
        world.GetOrCreateSystemManaged<StatResolveSystem>().Update();

        var expected = HeroCalculatedAttributes.Calculate(
            new HeroBaseStats { baseStrength = 12, baseDexterity = 9, baseArmor = 7, baseVitality = 11 },
            new EquipmentBonuses { vitalityBonus = 5, armorBonus = 3 }, EquipmentBonuses.Empty, classData);
        var health = em.GetComponentData<HeroHealthComponent>(hero);
        Assert.That(health.maxHealth, Is.EqualTo(expected.maxHealth).Within(1e-3f));
        Assert.That(health.currentHealth, Is.EqualTo(expected.maxHealth).Within(1e-3f), "A fresh hero starts at full health.");
        Assert.That(em.GetComponentData<StaminaComponent>(hero).maxStamina, Is.EqualTo(expected.stamina).Within(1e-3f));
        var defense = em.GetComponentData<DefenseComponent>(hero);
        Assert.That(defense.bluntDefense, Is.EqualTo(expected.bluntDefense).Within(1e-3f));
        Assert.That(defense.slashDefense, Is.EqualTo(expected.slashDefense).Within(1e-3f));
        Assert.That(defense.pierceDefense, Is.EqualTo(expected.pierceDefense).Within(1e-3f));
        var damage = em.GetComponentData<DamageProfileComponent>(hero);
        Assert.That(damage.bluntDamage, Is.EqualTo(expected.bluntDamage).Within(1e-3f));
        Assert.That(damage.slashingDamage, Is.EqualTo(expected.slashingDamage).Within(1e-3f));
        Assert.That(damage.piercingDamage, Is.EqualTo(expected.piercingDamage).Within(1e-3f));
        Assert.That(damage.slashingPenetration, Is.EqualTo(0f), "Heroes have no penetration in battle yet.");
    }

    [Test]
    public void AIHeroSpawnedWithOnlyItsClassUsesTheClassAttributesAndItsEquipment()
    {
        // In battle the class definition prefab bakes no class (its asset reference is broken), so remote and AI heroes
        // get their class at spawn (BattleSceneController) and must not depend on the baked class entity.
        var classData = HeroClassManager.GetClassDefinition(ClassId);
        using var world = new World("AI hero stats");
        var em = world.EntityManager;
        var hero = em.CreateEntity();
        em.AddComponentData(hero, new HeroClassComponent { heroClass = HeroClass.SwordAndShield });
        em.AddComponentData(hero, new HeroHealthComponent { maxHealth = 100f, currentHealth = 100f });
        em.AddComponentData(hero, new StaminaComponent { maxStamina = 100f, currentStamina = 100f });
        var helmet = new InventoryItem { itemId = "test_helmet", instanceId = "test-2" };
        helmet.GeneratedStats = new Dictionary<string, float> { ["Strength"] = 2f };
        em.AddComponentObject(hero, new HeroAppearanceComponent { equipment = new Equipment { helmet = helmet } });

        world.GetOrCreateSystemManaged<HeroInitializationSystem>().Update();
        world.GetOrCreateSystemManaged<StatResolveSystem>().Update();

        var expected = HeroCalculatedAttributes.Calculate(
            new HeroBaseStats
            {
                baseStrength = classData.baseStrength, baseDexterity = classData.baseDexterity,
                baseArmor = classData.baseArmor, baseVitality = classData.baseVitality,
            },
            new EquipmentBonuses { strengthBonus = 2 }, EquipmentBonuses.Empty, classData);
        Assert.That(em.GetComponentData<HeroHealthComponent>(hero).maxHealth, Is.EqualTo(expected.maxHealth).Within(1e-3f));
        Assert.That(em.GetComponentData<DamageProfileComponent>(hero).bluntDamage, Is.EqualTo(expected.bluntDamage).Within(1e-3f));
    }

    [Test]
    public void AttributeModifiersReachTheDerivedStatsAndDirectOnesStackOnTop()
    {
        var classData = HeroClassManager.GetClassDefinition(ClassId);
        using var world = new World("Hero modifiers");
        var em = world.EntityManager;
        var hero = em.CreateEntity();
        em.AddComponentData(hero, new HeroHealthComponent { maxHealth = 100f, currentHealth = 50f });
        em.AddComponentData(hero, HeroStatFormula.From(classData));
        StatStack.Ensure(em, hero);
        StatStack.SetBase(em, hero, StatId.Vitality, 10f);
        StatStack.Add(em, hero, StatId.Vitality, ModifierOp.Flat, 4f, ModifierSource.Equipment);
        StatStack.Add(em, hero, StatId.MaxHealth, ModifierOp.Ratio, 0.1f, ModifierSource.Perk);
        StatStack.MarkDirty(em, hero);
        var resolve = world.GetOrCreateSystemManaged<StatResolveSystem>();
        resolve.Update();

        float withArmour = classData.baseHealth + 14f * classData.healthPerVitality;
        var health = em.GetComponentData<HeroHealthComponent>(hero);
        Assert.That(health.maxHealth, Is.EqualTo(1.1f * withArmour).Within(1e-3f));
        Assert.That(health.currentHealth, Is.EqualTo(0.55f * withArmour).Within(1e-3f), "Half health stays half.");

        // Taking the equipment off drops the vitality it gave, and the health with it.
        StatStack.ClearSource(em, hero, ModifierSource.Equipment);
        StatStack.MarkDirty(em, hero);
        resolve.Update();
        float bare = classData.baseHealth + 10f * classData.healthPerVitality;
        Assert.That(em.GetComponentData<HeroHealthComponent>(hero).maxHealth, Is.EqualTo(1.1f * bare).Within(1e-3f));
    }
}
