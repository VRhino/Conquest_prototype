using NUnit.Framework;
using Unity.Entities;

/// <summary>Decision clock of the hero AI (L4-01).</summary>
public class AIThinkClockTests
{
    const float Interval = 0.1f;

    [Test]
    public void PhasesSpreadBotsWithinOneInterval()
    {
        using var world = new World("AI clock");
        var em = world.EntityManager;
        var seen = new System.Collections.Generic.HashSet<int>();
        for (int i = 0; i < 16; i++)
        {
            double phase = AIThinkClock.Phase(em.CreateEntity(), Interval);
            Assert.That(phase, Is.GreaterThanOrEqualTo(0).And.LessThan(Interval));
            seen.Add((int)(phase / Interval * 10)); // tenth of the interval it falls in
        }
        Assert.That(seen.Count, Is.GreaterThan(3), "Bots should not all think in the same slice.");
    }

    [Test]
    public void ThinkingKeepsThePhaseAtAnyFrameRate()
    {
        // Same simulated second at 30 and 144 fps: about ten thinks either way.
        foreach (float fps in new[] { 30f, 144f })
        {
            double next = 0.03, now = 0;
            int thinks = 0;
            for (int frame = 0; frame < fps; frame++)
            {
                now += 1.0 / fps;
                if (now < next) continue;
                thinks++;
                next = AIThinkClock.Next(next, now, Interval);
            }
            Assert.That(thinks, Is.InRange(9, 11), $"fps {fps}");
        }
    }

    [Test]
    public void ABotThatFellBehindRestartsInsteadOfBursting()
    {
        // Due at 1.0 but only ran at 5.0: the next think is one interval later, not a backlog of 40.
        Assert.That(AIThinkClock.Next(1.0, 5.0, Interval), Is.EqualTo(5.0 + Interval).Within(1e-9));
        Assert.That(AIThinkClock.Next(1.0, 1.02, Interval), Is.EqualTo(1.0 + Interval).Within(1e-9));
    }
}
