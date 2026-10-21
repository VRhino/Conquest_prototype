using NUnit.Framework;
using Unity.Mathematics;

/// <summary>Hit knock-back curve (pattern of the reference's hit_curve).</summary>
public class HitKnockbackTests
{
    const float Distance = 0.3f, Duration = 0.35f, Dt = 1f / 30f;

    [Test]
    public void PushCoversItsDistanceAndThenStops()
    {
        float travelled = 0f;
        for (float t = 0f; t < Duration + 0.5f; t += Dt)
            travelled += HitKnockback.Step(t, Dt, Duration, Distance);
        Assert.That(travelled, Is.EqualTo(Distance).Within(1e-4f));
        Assert.That(HitKnockback.Step(Duration + 0.1f, Dt, Duration, Distance), Is.EqualTo(0f));
    }

    [Test]
    public void PushStartsAndEndsAtRestBelowTheJoltThreshold()
    {
        float peak = HitKnockback.PeakAcceleration(Duration, Distance);
        Assert.That(peak, Is.LessThan(MovementMetricsAnalyzer.AccelerationSpikeLimit));
        // Frame-to-frame speed changes stay within the curve's peak acceleration.
        float previous = 0f, maxChange = 0f;
        for (float t = 0f; t < Duration + Dt; t += Dt)
        {
            float speed = HitKnockback.Step(t, Dt, Duration, Distance) / Dt;
            maxChange = math.max(maxChange, math.abs(speed - previous) / Dt);
            previous = speed;
        }
        Assert.That(maxChange, Is.LessThanOrEqualTo(peak * 1.05f));
    }

    [Test]
    public void ZeroDistanceOrDurationIsOff()
    {
        Assert.That(HitKnockback.Step(0f, Dt, Duration, 0f), Is.EqualTo(0f));
        Assert.That(HitKnockback.Step(0f, Dt, 0f, Distance), Is.EqualTo(0f));
    }
}
