using NUnit.Framework;
using Unity.Entities;

/// <summary>Random damage variance rolled with the seeded hit RNG (Aprendizajes_Referencia.md L1-12).</summary>
public class DamageVarianceTests
{
    static readonly Entity Attacker = new Entity { Index = 7, Version = 1 };
    static readonly Entity TargetA = new Entity { Index = 11, Version = 1 };
    static readonly Entity TargetB = new Entity { Index = 12, Version = 1 };

    [Test]
    public void TheRollIsReproducibleBoundedAndApartPerTarget()
    {
        Assert.That(CombatRandom.RollVariance(1000, Attacker, TargetA, 0f), Is.EqualTo(0f), "No variance: no roll.");
        float first = CombatRandom.RollVariance(1000, Attacker, TargetA, 0.2f);
        Assert.That(CombatRandom.RollVariance(1000, Attacker, TargetA, 0.2f), Is.EqualTo(first), "Same hit, same roll.");
        bool differs = false;
        for (uint tick = 0; tick < 200; tick++)
        {
            float a = CombatRandom.RollVariance(tick, Attacker, TargetA, 0.2f);
            Assert.That(a, Is.InRange(-0.2f, 0.2f));
            differs |= a != CombatRandom.RollVariance(tick, Attacker, TargetB, 0.2f);
        }
        Assert.That(differs, Is.True, "Two targets of one area hit roll apart.");
        Assert.That(CombatRandom.RollVariance(1, Attacker, TargetA, 5f), Is.InRange(-1f, 1f), "Variance clamped to 1.");
    }

    [Test]
    public void TheFormulaMultipliesTheFinalDamageByTheRolledFactor()
    {
        var input = new DamageInput
        {
            profile = new DamageProfileComponent { slashingDamage = 100f },
            multiplier = 1f, kineticMultiplier = -1f, varianceOffset = -0.1f,
        };
        var r = DamageFormula.Compute(input, CombatTuning.Default);
        Assert.That(r.varianceFactor, Is.EqualTo(0.9f).Within(1e-5f));
        Assert.That(r.effective, Is.EqualTo(90f).Within(1e-3f));
        input.varianceOffset = 0f;
        Assert.That(DamageFormula.Compute(input, CombatTuning.Default).effective, Is.EqualTo(100f).Within(1e-3f));
    }
}
