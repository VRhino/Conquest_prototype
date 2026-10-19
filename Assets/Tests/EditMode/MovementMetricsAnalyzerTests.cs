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

    static MovementUnitSample Unit(int frame, float time, int unit, UnitFormationState state) =>
        new MovementUnitSample { frame = frame, time = time, squad = 1, unit = unit, state = state };
}
