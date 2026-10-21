using NUnit.Framework;
using Unity.Entities;

/// <summary>Critical hits as in the reference (Aprendizajes_Referencia.md L1-04).</summary>
public class CritFormulaTests
{
    static readonly float Floor = CombatTuning.Default.criticalExtraFloor;

    static CritStatsComponent FromWeapon(float chance, float multiplier) => new CritStatsComponent
    {
        critValue = CritFormula.ValueForChance(chance), critParameter = CritFormula.UnitParameter,
        critDamageValue = CritFormula.DamageValueForMultiplier(multiplier), critDamageParameter = CritFormula.UnitParameter,
        antiCritParameter = CritFormula.UnitParameter,
    };

    [TestCase(0f)]
    [TestCase(0.05f)]
    [TestCase(0.1f)]
    [TestCase(0.5f)]
    public void WeaponBasesKeepTodaysChance(float chance)
        => Assert.That(CritFormula.Rate(FromWeapon(chance, 1.5f)), Is.EqualTo(chance).Within(1e-6f));

    [Test]
    public void WeaponBasesKeepTodaysMultiplierAboveTheFloor()
    {
        Assert.That(CritFormula.Multiplier(FromWeapon(0.05f, 1.5f), CritFormula.NoAntiCrit, Floor), Is.EqualTo(1.5f).Within(1e-6f));
        Assert.That(CritFormula.Multiplier(FromWeapon(0.05f, 1.1f), CritFormula.NoAntiCrit, Floor),
            Is.EqualTo(1f + Floor).Within(1e-6f), "A critical always adds at least the floor.");
    }

    [Test]
    public void RateIsTheAttackersAndAntiCriticalOnlyCutsTheExtraDamage()
    {
        var attacker = new CritStatsComponent { critValue = 30f, critParameter = 70f, critDamageValue = 80f, critDamageParameter = 100f, critDamageRatio = 0.1f };
        var defender = new CritStatsComponent { antiCritValue = 20f, antiCritParameter = 100f, critValue = 999f };
        Assert.That(CritFormula.Rate(attacker), Is.EqualTo(0.3f).Within(1e-6f));
        // 80/100 − 20/100 + 0.1 = 0.7
        Assert.That(CritFormula.Extra(attacker, defender, Floor), Is.EqualTo(0.7f).Within(1e-6f));
        var tank = new CritStatsComponent { antiCritValue = 200f, antiCritParameter = 100f };
        Assert.That(CritFormula.Extra(attacker, tank, Floor), Is.EqualTo(Floor), "Never below the floor.");
        Assert.That(CritFormula.Rate(new CritStatsComponent { critParameter = 50f }), Is.EqualTo(0f), "No value, no critical.");
    }

    [Test]
    public void ResolverWritesCritStatsAndModifiersRaiseTheRate()
    {
        using var world = new World("Crit stats");
        var em = world.EntityManager;
        var unit = em.CreateEntity();
        StatStack.Ensure(em, unit);
        StatStack.SeedCritical(em, unit, 0.05f, 1.5f);
        StatStack.Add(em, unit, StatId.CritValue, ModifierOp.Ratio, 1f, ModifierSource.Perk);
        StatStack.MarkDirty(em, unit);
        world.GetOrCreateSystemManaged<StatResolveSystem>().Update();

        var crit = em.GetComponentData<CritStatsComponent>(unit);
        float doubled = 2f * CritFormula.ValueForChance(0.05f);
        Assert.That(CritFormula.Rate(crit), Is.EqualTo(doubled / (1f + doubled)).Within(1e-6f));
        Assert.That(CritFormula.Multiplier(crit, CritFormula.NoAntiCrit, Floor), Is.EqualTo(1.5f).Within(1e-6f));
    }
}
