using NUnit.Framework;

/// <summary>Hysteresis gate of the hero AI (L4-02).</summary>
public class DebouncedGateTests
{
    const float Open = 0.3f, Close = 1f, Dt = 0.1f;

    static DebouncedGate Run(DebouncedGate gate, bool raw, float seconds)
    {
        for (float t = 0f; t < seconds - 1e-4f; t += Dt) gate = DebouncedGate.Step(gate, raw, Dt, Open, Close);
        return gate;
    }

    [Test]
    public void OpensOnlyAfterTheConditionHolds()
    {
        var gate = Run(default, true, 0.2f);
        Assert.That(gate.open, Is.False);
        gate = Run(gate, true, 0.1f);
        Assert.That(gate.open, Is.True);
    }

    [Test]
    public void OneTickOfProximityDoesNotFlipTheBot()
    {
        var gate = Run(default, true, 0.1f);
        gate = Run(gate, false, 0.1f); // The blip ends: the pending time starts over.
        gate = Run(gate, true, 0.2f);
        Assert.That(gate.open, Is.False);
    }

    [Test]
    public void StaysOpenThroughShortGapsAndClosesAfterTheCloseTime()
    {
        var gate = Run(default, true, 0.3f);
        Assert.That(gate.open, Is.True);
        gate = Run(gate, false, 0.9f);
        Assert.That(gate.open, Is.True);
        gate = Run(gate, false, 0.1f);
        Assert.That(gate.open, Is.False);
    }
}
