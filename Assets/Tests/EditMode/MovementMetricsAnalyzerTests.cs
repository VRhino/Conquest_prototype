using System.Collections.Generic;
using NUnit.Framework;

public class MovementMetricsAnalyzerTests
{
    const float Dt = 0.1f;

    [Test]
    public void StopAndStartEventsProduceArrivalAndDepartureMetrics()
    {
        var frames = new List<MovementFrameSample>();
        var units = new List<MovementUnitSample>();
        for (int f = 0; f <= 60; f++)
        {
            float t = f * Dt;
            bool moving = t < 1f || t >= 4f;           // Stop at 1.0 s, start again at 4.0 s.
            frames.Add(new MovementFrameSample { frame = f, time = t, squad = 1, anchorMoving = moving, setDestinationCalls = 1 });
            units.Add(Unit(f, t, 0, t < 1.5f ? UnitFormationState.Moving
                : t < 4.1f ? UnitFormationState.Formed : UnitFormationState.Moving));
            // Unit 1 oscillates once after arriving: Formed, Waiting, Formed.
            units.Add(Unit(f, t, 1, t < 1.4f ? UnitFormationState.Moving
                : t < 1.8f ? UnitFormationState.Formed
                : t < 2.0f ? UnitFormationState.Waiting
                : t < 4.3f ? UnitFormationState.Formed : UnitFormationState.Moving));
        }

        var summary = MovementMetricsAnalyzer.Analyze("test", frames, units);

        Assert.That(summary.stops, Is.EqualTo(1));
        Assert.That(summary.starts, Is.EqualTo(1));
        Assert.That(summary.units, Is.EqualTo(2));
        Assert.That(summary.maxExtraTransitionsAfterArrival, Is.EqualTo(2)); // Formed->Waiting->Formed.
        Assert.That(summary.maxTimeToFormed, Is.EqualTo(0.5f).Within(1e-4f));
        Assert.That(summary.meanStartSpread, Is.EqualTo(0.2f).Within(1e-4f));
        Assert.That(summary.maxLastStart, Is.EqualTo(0.3f).Within(1e-4f));
        Assert.That(summary.setDestinationPerUnitPerSecond, Is.EqualTo(61f / (2 * 6f)).Within(1e-3f));
    }

    [Test]
    public void SingleFrameAnchorPauseIsNotAStop()
    {
        var frames = new List<MovementFrameSample>();
        for (int f = 0; f <= 20; f++)
            frames.Add(new MovementFrameSample { frame = f, time = f * Dt, squad = 1, anchorMoving = f != 10 });
        var units = new List<MovementUnitSample> { Unit(0, 0f, 0, UnitFormationState.Moving) };

        Assert.That(MovementMetricsAnalyzer.Analyze("pause", frames, units).stops, Is.EqualTo(0));
    }

    [Test]
    public void ContactMetricsCountAttackersLeashAndTurnRate()
    {
        var frames = new List<MovementFrameSample>();
        var units = new List<MovementUnitSample>();
        for (int f = 0; f <= 20; f++)
        {
            float t = f * Dt;
            frames.Add(new MovementFrameSample { frame = f, time = t, deltaTime = Dt, squad = 1,
                allyOverlaps = f < 10 ? 2 : 0, persistentEnemyOverlaps = f == 15 ? 1 : 0 });
            // Unit 0 attacks target 7 from 1.3 m and snaps 90° in one frame at f = 5.
            var a = Unit(f, t, 0, UnitFormationState.Moving);
            a.target = 7; a.targetDistance = 1.3f; a.attacking = true; a.yaw = f >= 5 ? 1.5708f : 0f;
            // Unit 1 also on target 7, reacting and 8 m from its slot for the whole run (one leash violation).
            var b = Unit(f, t, 1, UnitFormationState.Moving);
            b.target = 7; b.targetDistance = 2f; b.reacting = true; b.slotDistance = 8f;
            units.Add(a); units.Add(b);
        }

        var s = MovementMetricsAnalyzer.Analyze("contact", frames, units);

        Assert.That(s.maxAttackersPerTarget, Is.EqualTo(2));
        Assert.That(s.meanContactDistance, Is.EqualTo(1.3f).Within(1e-4f));
        Assert.That(s.leashViolations, Is.EqualTo(1));
        Assert.That(s.turnRateViolationFrames, Is.EqualTo(1));
        Assert.That(s.maxAllyOverlaps, Is.EqualTo(2));
        Assert.That(s.maxPersistentEnemyOverlaps, Is.EqualTo(1));
    }

    static MovementUnitSample Unit(int frame, float time, int unit, UnitFormationState state) =>
        new MovementUnitSample { frame = frame, time = time, squad = 1, unit = unit, state = state,
            target = -1, targetDistance = -1f, gear = -1 };

    [Test]
    public void MedianCombinesRunsFieldByField()
    {
        var runs = new List<MovementMetricsSummary>
        {
            new MovementMetricsSummary { label = "S6", maxPersistentEnemyOverlaps = 8, accelerationSpikeRatio = 0.03f, maxAcceleration = 280f },
            new MovementMetricsSummary { label = "S6", maxPersistentEnemyOverlaps = 15, accelerationSpikeRatio = 0.01f, maxAcceleration = 110f },
            new MovementMetricsSummary { label = "S6", maxPersistentEnemyOverlaps = 11, accelerationSpikeRatio = 0.02f, maxAcceleration = 200f },
        };
        var median = MovementMetricsAnalyzer.Median(runs);
        Assert.That(median.maxPersistentEnemyOverlaps, Is.EqualTo(11));
        Assert.That(median.accelerationSpikeRatio, Is.EqualTo(0.02f).Within(1e-6f));
        Assert.That(median.maxAcceleration, Is.EqualTo(200f));
        Assert.That(median.label, Does.StartWith("S6"));

        runs.RemoveAt(2); // Even count: mean of the two middle values.
        Assert.That(MovementMetricsAnalyzer.Median(runs).maxAcceleration, Is.EqualTo(195f).Within(1e-3f));
    }
}
