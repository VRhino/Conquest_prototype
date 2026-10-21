using NUnit.Framework;
using Unity.Entities;
using UnityEngine;

/// <summary>Skills as data (Aprendizajes_Referencia.md L2-2) and the hero's skill slots in battle.</summary>
public class SkillDataTests
{
    [Test]
    public void AbilityWithoutPhasesGetsWindupActiveRecoveryAndItsAreaHit()
    {
        var ability = ScriptableObject.CreateInstance<HeroAbility>();
        ability.abilityName = "Test Spin";
        ability.category = HeroAbilityCategory.E;
        ability.cooldown = 8f;
        ability.staminaCost = 20f;
        ability.areaRadius = 3f;
        ability.damageMultiplier = 1.5f;
        var blob = SkillBlobBuilder.Build(ability);
        try
        {
            ref var skill = ref blob.Value;
            Assert.That(skill.id, Is.EqualTo(SkillBlobBuilder.IdOf(ability)));
            Assert.That(skill.cooldownGroup, Is.EqualTo(SkillBlobBuilder.DefaultGroup(SkillSlot.E)));
            Assert.That(skill.phases.Length, Is.EqualTo(3));
            Assert.That(skill.phases[1].interruptible, Is.False, "The active phase protects itself.");
            Assert.That(skill.phases[2].final, Is.True);
            Assert.That(skill.events.Length, Is.EqualTo(2));
            Assert.That(skill.events[1].type, Is.EqualTo(SkillEventType.AreaDamage));
            Assert.That(skill.events[1].radius, Is.EqualTo(3f));
            Assert.That(skill.events[1].value, Is.EqualTo(1.5f));
        }
        finally
        {
            blob.Dispose();
            Object.DestroyImmediate(ability);
        }
    }

    [Test]
    public void BasicAttackKeepsTheWeaponStrikeWindow()
    {
        var combat = new HeroCombatComponent { strikeWindowStart = 0.2f, strikeWindowDuration = 0.15f, attackAnimationDuration = 0.8f };
        var blob = SkillBlobBuilder.BuildBasicAttack(combat, 15f, 0.7f);
        try
        {
            ref var skill = ref blob.Value;
            Assert.That(skill.phases[0].duration, Is.EqualTo(0.2f).Within(1e-6f));
            Assert.That(skill.phases[1].duration, Is.EqualTo(0.15f).Within(1e-6f));
            Assert.That(skill.phases[2].duration, Is.EqualTo(0.45f).Within(1e-6f));
            Assert.That(skill.events[1].type, Is.EqualTo(SkillEventType.HitboxOpen));
            Assert.That(skill.staminaCost, Is.EqualTo(15f));
        }
        finally
        {
            blob.Dispose();
        }
    }

    [Test]
    public void BattleHeroGetsTheBasicAttackAndTheFourClassAbilities()
    {
        using var world = new World("Hero skills");
        var em = world.EntityManager;
        em.CreateSingleton(new HeroGameplayConfigComponent { attackStaminaCost = 15f, attackCooldown = 0.7f });
        var hero = em.CreateEntity();
        em.AddComponentData(hero, new HeroClassComponent { heroClass = HeroClass.SwordAndShield });
        em.AddComponentData(hero, new HeroHealthComponent { maxHealth = 100f, currentHealth = 100f });
        em.AddComponentData(hero, new StaminaComponent { maxStamina = 100f, currentStamina = 100f });
        em.AddComponentData(hero, new HeroCombatComponent { strikeWindowStart = 0.2f, strikeWindowDuration = 0.15f, attackAnimationDuration = 0.8f });

        world.GetOrCreateSystemManaged<HeroInitializationSystem>().Update();

        var skills = em.GetBuffer<HeroSkillElement>(hero);
        Assert.That(skills.Length, Is.EqualTo(5));
        Assert.That(skills[0].slot, Is.EqualTo(SkillSlot.Basic));
        var slots = new System.Collections.Generic.HashSet<SkillSlot>();
        foreach (var s in skills) slots.Add(s.slot);
        Assert.That(slots, Is.EquivalentTo(new[] { SkillSlot.Basic, SkillSlot.Q, SkillSlot.E, SkillSlot.R, SkillSlot.Ultimate }));
        Assert.That(em.HasBuffer<CooldownGroupElement>(hero), Is.True);
        Assert.That(em.GetComponentData<CooldownScaleComponent>(hero).value, Is.EqualTo(1f));
    }
}
