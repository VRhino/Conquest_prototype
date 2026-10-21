using NUnit.Framework;
using Unity.Entities;

/// <summary>Seeded combat randomness (L1-05).</summary>
public class CombatRandomTests
{
    [Test]
    public void SameHitRollsTheSameAndDifferentHitsDiffer()
    {
        using var world = new World("Combat random");
        var a = world.EntityManager.CreateEntity();
        var b = world.EntityManager.CreateEntity();
        uint tick = CombatRandom.Tick(12.345);
        float first = CombatRandom.ForHit(tick, a, CombatRandom.CriticalSalt).NextFloat();
        Assert.That(CombatRandom.ForHit(tick, a, CombatRandom.CriticalSalt).NextFloat(), Is.EqualTo(first));
        Assert.That(CombatRandom.ForHit(tick, b, CombatRandom.CriticalSalt).NextFloat(), Is.Not.EqualTo(first));
        Assert.That(CombatRandom.ForHit(tick + 1, a, CombatRandom.CriticalSalt).NextFloat(), Is.Not.EqualTo(first));
        Assert.That(CombatRandom.ForHit(tick, a, CombatRandom.SpreadSalt).NextFloat(), Is.Not.EqualTo(first));
    }

    [Test]
    public void CriticalRateMatchesTheChance()
    {
        using var world = new World("Combat random rate");
        var attacker = world.EntityManager.CreateEntity();
        int crits = 0;
        const int Rolls = 4000;
        for (uint t = 0; t < Rolls; t++)
            if (CombatRandom.RollCritical(t, attacker, 0.25f)) crits++;
        Assert.That(crits / (float)Rolls, Is.EqualTo(0.25f).Within(0.03f));
        Assert.That(CombatRandom.RollCritical(1, attacker, 0f), Is.False);
        Assert.That(CombatRandom.RollCritical(1, attacker, 1f), Is.True);
    }
}
