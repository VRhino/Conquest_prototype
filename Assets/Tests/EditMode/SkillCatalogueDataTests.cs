using System.Linq;
using NUnit.Framework;
using UnityEditor;

/// <summary>
/// The catalogue data that uses the skill effects L2-10 to L2-14 (Docs/Por_Abordar/Habilidades_Perks.md H15, table
/// approved 2026-11-04): caps, stacks, attack speed phases and the stats that scale them.
/// </summary>
public class SkillCatalogueDataTests
{
    const string Abilities = "Assets/ScriptableObjects/Hero/Abilities/";
    const string Perks = "Assets/ScriptableObjects/Hero/Perks/";

    static HeroAbility Ability(string name) => AssetDatabase.LoadAssetAtPath<HeroAbility>(Abilities + name + ".asset");
    static HeroPerk Perk(string name) => AssetDatabase.LoadAssetAtPath<HeroPerk>(Perks + name + ".asset");

    [TestCase("TwoHandedSword", 6)]
    [TestCase("Bow2", 8)]
    [TestCase("Spear1", 5)]
    [TestCase("Spear4", 10)]
    public void AreaSkillsHitAtMostTheirCap(string asset, int cap)
    {
        var damage = Ability(asset).events.Where(e => e.type == SkillEventType.AreaDamage).ToArray();
        Assert.That(damage, Is.Not.Empty);
        Assert.That(damage.All(e => e.maxTargets == cap), Is.True);
    }

    [Test]
    public void WideSweepSlowsEveryoneItReaches()
    {
        var slow = Ability("Spear1").events.Single(e => e.type == SkillEventType.ApplyModifier);
        Assert.That(slow.maxTargets, Is.Zero, "Only its damage is capped: it still stops every charging enemy.");
        Assert.That(Ability("Spear4").events.Single(e => e.type == SkillEventType.ApplyModifier).maxTargets, Is.EqualTo(10));
    }

    [Test]
    public void FlamingArrowsBurnStacksToTwo()
    {
        var burn = Ability("Bow4").events.Single(e => e.type == SkillEventType.DamageOverTime);
        Assert.That(burn.maxStacks, Is.EqualTo(2));
        Assert.That(burn.reapply, Is.EqualTo(EffectReapply.Stack));
    }

    [TestCase("TwoHandedSword 2")]
    [TestCase("TwoHandedSword")]
    [TestCase("Spear1")]
    public void WeaponSwingSkillsFollowTheAttackSpeed(string asset)
    {
        using var blob = SkillBlobBuilder.Build(Ability(asset));
        Assert.That(blob.Value.phases.Length, Is.GreaterThan(0));
        for (int i = 0; i < blob.Value.phases.Length; i++)
            Assert.That(blob.Value.phases[i].scalesWithAttackSpeed, Is.True, asset + " phase " + i);
    }

    [Test]
    public void JudgmentOfSteelSpeedsUpItsSquad()
    {
        var speed = Ability("TwoHandedSword 3").events.Single(e => e.stat == StatId.AttackSpeed);
        Assert.That(speed.type, Is.EqualTo(SkillEventType.ApplyModifier));
        Assert.That(speed.target, Is.EqualTo(SkillTarget.OwnSquad));
        Assert.That(speed.op, Is.EqualTo(ModifierOp.Ratio));
        Assert.That(speed.value, Is.EqualTo(0.15f).Within(1e-5f));
        Assert.That(speed.duration, Is.EqualTo(8f));
    }

    [Test]
    public void PerksScaleTheDurationOfEffects()
    {
        var line = Perk("ShieldAndSwordPerk1").effects.Single(e => e.stat == StatId.EffectDurationIn);
        Assert.That(line.target, Is.EqualTo(PerkTarget.Self));
        Assert.That(line.condition, Is.EqualTo(PerkCondition.HeroStill), "Like the rest of Unbreakable Line.");
        Assert.That(line.value, Is.EqualTo(-0.3f).Within(1e-5f));
        var mark = Perk("BowPerk2").effects.Single(e => e.stat == StatId.EffectDurationOut);
        Assert.That(mark.target, Is.EqualTo(PerkTarget.Self));
        Assert.That(mark.value, Is.EqualTo(0.2f).Within(1e-5f));
    }

    /// <summary>L2-17, approved 2026-11-06: Shield Bash runs per soldier, a 90° cone of 1.8 m in front of each, one enemy.</summary>
    [Test]
    public void ShieldBashRunsPerSoldierInASmallConeInFront()
    {
        var bash = AssetDatabase.LoadAssetAtPath<AbilityData>("Assets/ScriptableObjects/Squads/Abilities/Squires_ShieldBash.asset");
        Assert.That(bash.perSoldier, Is.True);
        Assert.That(bash.events, Is.Not.Empty);
        foreach (var e in bash.events)
        {
            Assert.That(e.radius, Is.EqualTo(1.8f).Within(1e-5f));
            Assert.That(e.angle, Is.EqualTo(90f));
            Assert.That(e.distance, Is.EqualTo(0f));
            Assert.That(e.maxTargets, Is.EqualTo(1));
        }
    }

    /// <summary>L2-16, approved 2026-11-06: the ultimates ask for and spend 20 combo; nothing costs kills yet.</summary>
    [TestCase("Bow4")]
    [TestCase("Spear4")]
    [TestCase("SwordAndShield4")]
    [TestCase("TwoHandedSword 3")]
    public void UltimatesCostTwentyCombo(string asset)
    {
        var ability = Ability(asset);
        Assert.That(ability.category, Is.EqualTo(HeroAbilityCategory.Ultimate));
        Assert.That(ability.comboCost, Is.EqualTo(20f));
        Assert.That(ability.killCost, Is.EqualTo(0));
    }
}
