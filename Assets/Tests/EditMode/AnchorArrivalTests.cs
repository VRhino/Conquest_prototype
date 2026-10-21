using NUnit.Framework;
using Unity.Mathematics;

/// <summary>Arrival of the squad anchor (C10, Movimiento_Formaciones.md L-1).</summary>
public class AnchorArrivalTests
{
    const float Dt = 1f / 30f, Accel = 8f, Braking = 2.5f, Snap = 10f;

    [Test]
    public void ZeroAccelerationCopiesTheTarget()
    {
        float3 v = new float3(1f, 0f, 0f);
        var target = new float3(3f, 0.5f, -2f);
        Assert.That(AnchorArrival.Step(float3.zero, ref v, target, 0f, Braking, Snap, Dt), Is.EqualTo(target));
        Assert.That(v, Is.EqualTo(float3.zero));
    }

    [Test]
    public void LargeJumpSnaps()
    {
        float3 v = float3.zero;
        var target = new float3(50f, 0f, 0f);
        Assert.That(AnchorArrival.Step(float3.zero, ref v, target, Accel, Braking, Snap, Dt), Is.EqualTo(target));
    }

    [Test]
    public void StartsAndStopsWithinItsAccelerationAndNeverOvershoots()
    {
        // The hero marches at 3 m/s for 4 s and stops dead.
        float3 hero = float3.zero, anchor = float3.zero, v = float3.zero;
        float previousSpeed = 0f, maxChange = 0f, maxBeyond = 0f;
        for (int frame = 0; frame < 300; frame++)
        {
            if (frame < 120) hero.z += 3f * Dt;
            anchor = AnchorArrival.Step(anchor, ref v, hero, Accel, Braking, Snap, Dt);
            float speed = math.length(v);
            maxChange = math.max(maxChange, math.abs(speed - previousSpeed) / Dt);
            previousSpeed = speed;
            maxBeyond = math.max(maxBeyond, anchor.z - hero.z);
        }
        // The last step lands on the target: allow 1 % over the braking limit for it.
        Assert.That(maxChange, Is.LessThanOrEqualTo(Accel * Braking * 1.01f));
        Assert.That(maxBeyond, Is.LessThanOrEqualTo(1e-4f), "The anchor passed the hero.");
        Assert.That(anchor, Is.EqualTo(hero), "The anchor did not settle on the stopped hero.");
    }

    [Test]
    public void SteadyMarchTrailsByTheBrakingDistance()
    {
        float3 hero = float3.zero, anchor = float3.zero, v = float3.zero;
        for (int frame = 0; frame < 180; frame++)
        {
            hero.z += 3f * Dt;
            anchor = AnchorArrival.Step(anchor, ref v, hero, Accel, Braking, Snap, Dt);
        }
        Assert.That(math.length(v), Is.EqualTo(3f).Within(0.05f));
        Assert.That(hero.z - anchor.z, Is.EqualTo(AnchorArrival.SteadyLag(3f, Accel)).Within(0.1f));
    }
}
