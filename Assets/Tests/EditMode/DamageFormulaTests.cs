using NUnit.Framework;

/// <summary>Pure damage formula (L1-03; Docs/Mechanics/DamageMechanics.md §5).</summary>
public class DamageFormulaTests
{
    static readonly CombatTuning T = CombatTuning.Default;

    static DamageInput Slash(float damage, float defense, float penetration = 0f) => new DamageInput
    {
        profile = new DamageProfileComponent { slashingDamage = damage, slashingPenetration = penetration },
        multiplier = 1f,
        defense = new DefenseComponent { slashDefense = defense },
        kineticMultiplier = -1f,
    };

    [Test]
    public void FlatReductionWithAFivePercentFloorPerType()
    {
        Assert.That(DamageFormula.Compute(Slash(100f, 30f), T).effective, Is.EqualTo(70f).Within(1e-4f));
        Assert.That(DamageFormula.Compute(Slash(100f, 500f), T).effective, Is.EqualTo(5f).Within(1e-4f));
        // Penetration cuts the defense, never below zero.
        Assert.That(DamageFormula.Compute(Slash(100f, 30f, 50f), T).effective, Is.EqualTo(100f).Within(1e-4f));
    }

    [Test]
    public void TypesWithoutDamageDoNotGetTheFloor()
    {
        var input = Slash(100f, 0f);
        input.defense.bluntDefense = 1000f; // No blunt damage: no 5 % of nothing.
        var r = DamageFormula.Compute(input, T);
        Assert.That(r.blunt, Is.EqualTo(0f));
        Assert.That(r.effective, Is.EqualTo(100f).Within(1e-4f));
    }

    [Test]
    public void KineticAndHeightBonusesMultiply()
    {
        var input = Slash(100f, 0f);
        input.attackerSpeed = 5f;          // Full reference speed, default multiplier 0.3: ×1.3.
        input.heightDelta = 2f;            // Above the 0.5 m threshold: ×1.2.
        var r = DamageFormula.Compute(input, T);
        Assert.That(r.kineticBonus, Is.EqualTo(1.3f).Within(1e-5f));
        Assert.That(r.heightBonus, Is.EqualTo(1.2f).Within(1e-5f));
        Assert.That(r.effective, Is.EqualTo(156f).Within(1e-3f));

        input.heightDelta = 0.4f;          // Below the threshold: no height bonus.
        input.kineticMultiplier = 0.5f;    // The weapon's own multiplier wins over the default.
        r = DamageFormula.Compute(input, T);
        Assert.That(r.heightBonus, Is.EqualTo(1f));
        Assert.That(r.kineticBonus, Is.EqualTo(1.5f).Within(1e-5f));
    }

    [Test]
    public void CriticalMultipliesTheRawDamageBeforeDefense()
    {
        var input = Slash(100f, 30f);
        input.multiplier = 1.5f;
        Assert.That(DamageFormula.Compute(input, T).effective, Is.EqualTo(120f).Within(1e-4f));
    }
}
