using NUnit.Framework;
using Unity.Mathematics;

/// <summary>Charge bonus by the run-up towards the target, with threshold and cap per weapon (Aprendizajes_Referencia.md L1-11).</summary>
public class ChargeBonusTests
{
    static readonly HitTraits Lance = new HitTraits { chargeThreshold = 2f, chargeFullDistance = 6f, chargeMaxBonus = 0.5f };

    [Test]
    public void SamplesMoveOnEveryHalfWindow()
    {
        var track = new ChargeTrackComponent();
        ChargeTrack.Sample(ref track, new float3(0, 0, 0), 0.0, 1f);
        Assert.That(track.initialised, Is.True);
        ChargeTrack.Sample(ref track, new float3(1, 0, 0), 0.3, 1f);
        Assert.That(track.newer.x, Is.EqualTo(0f), "Not half a window yet.");
        ChargeTrack.Sample(ref track, new float3(2, 0, 0), 0.5, 1f);
        Assert.That(track.older.x, Is.EqualTo(0f));
        Assert.That(track.newer.x, Is.EqualTo(2f));
        ChargeTrack.Sample(ref track, new float3(4, 0, 0), 1.0, 1f);
        Assert.That(track.older.x, Is.EqualTo(2f));
        Assert.That(track.newer.x, Is.EqualTo(4f));
    }

    [Test]
    public void OnlyTheRunUpTowardsTheTargetCounts()
    {
        var track = new ChargeTrackComponent { older = float3.zero, initialised = true };
        var position = new float3(5, 0, 0);
        Assert.That(ChargeTrack.Approach(track, position, new float3(10, 0, 0)), Is.EqualTo(5f).Within(1e-4f));
        Assert.That(ChargeTrack.Approach(track, position, new float3(5, 0, 10)), Is.EqualTo(0f).Within(1e-4f),
            "Running past the target sideways is no charge.");
        Assert.That(ChargeTrack.Approach(track, position, new float3(-10, 0, 0)), Is.EqualTo(0f), "Running away: none.");
        Assert.That(ChargeTrack.Approach(track, new float3(5, 3, 0), new float3(10, 0, 0)), Is.EqualTo(5f).Within(1e-4f),
            "Measured on the ground plane.");
        Assert.That(ChargeTrack.Approach(default, position, new float3(10, 0, 0)), Is.EqualTo(0f), "No samples yet.");
    }

    [Test]
    public void TheBonusNeedsTheThresholdAndIsCappedByTheWeapon()
    {
        Assert.That(ChargeTrack.Bonus(2f, Lance), Is.EqualTo(1f), "At the threshold: nothing.");
        Assert.That(ChargeTrack.Bonus(3f, Lance), Is.EqualTo(1.25f).Within(1e-5f), "Weight 3/6.");
        Assert.That(ChargeTrack.Bonus(12f, Lance), Is.EqualTo(1.5f).Within(1e-5f), "Capped at the weapon's maximum.");
        Assert.That(ChargeTrack.Bonus(12f, new HitTraits()), Is.EqualTo(1f), "A weapon without charge never charges.");
    }

    [Test]
    public void TheFormulaMultipliesTheChargeBonus()
    {
        var input = new DamageInput
        {
            profile = new DamageProfileComponent { piercingDamage = 100f, traits = Lance },
            multiplier = 1f, kineticMultiplier = -1f, chargeDistance = 6f,
        };
        var r = DamageFormula.Compute(input, CombatTuning.Default);
        Assert.That(r.chargeBonus, Is.EqualTo(1.5f).Within(1e-5f));
        Assert.That(r.effective, Is.EqualTo(150f).Within(1e-3f));
    }
}
