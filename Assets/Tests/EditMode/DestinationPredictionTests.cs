using NUnit.Framework;
using Unity.Mathematics;

public class DestinationPredictionTests
{
    const float Dt = 1f / 60f;

    [Test]
    public void PredictionNeverExceedsTheMaximumLead()
    {
        var predicted = FormationDestinationPrediction.Predict(float3.zero, new float3(0, 0, 10), 0.4f, 1f);
        Assert.That(math.length(predicted), Is.EqualTo(1f).Within(1e-4f));
    }

    [Test]
    public void StillSlotPredictsTheSlotItself()
    {
        var slot = new float3(3, 1, -2);
        Assert.That(FormationDestinationPrediction.Predict(slot, float3.zero, 0.4f, 1f), Is.EqualTo(slot));
    }

    [Test]
    public void SteadyMarchConvergesToTheSlotSpeed()
    {
        float3 previous = default, velocity = default;
        bool has = false;
        for (int i = 0; i < 120; i++) // 2 s at 3 m/s along z.
            FormationDestinationPrediction.UpdateSlotVelocity(ref previous, ref has, ref velocity,
                new float3(0, 0, 3f * Dt * i), Dt, 0.15f, 2f);
        Assert.That(velocity.z, Is.EqualTo(3f).Within(0.05f));
        Assert.That(velocity.y, Is.EqualTo(0f));
    }

    [Test]
    public void SlotJumpResetsTheVelocity()
    {
        float3 previous = default, velocity = new float3(0, 0, 3);
        bool has = true;
        FormationDestinationPrediction.UpdateSlotVelocity(ref previous, ref has, ref velocity,
            new float3(0, 0, 5), Dt, 0.15f, 2f);
        Assert.That(velocity, Is.EqualTo(float3.zero));
    }

    [Test]
    public void ChangeThresholdGrowsWithSpeedButKeepsItsMinimum()
    {
        Assert.That(FormationDestinationPrediction.ChangeThreshold(float3.zero, 0.25f, 0.05f), Is.EqualTo(0.25f));
        Assert.That(FormationDestinationPrediction.ChangeThreshold(new float3(10, 0, 0), 0.25f, 0.05f),
            Is.EqualTo(0.5f).Within(1e-4f));
    }
}
