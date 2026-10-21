using NUnit.Framework;
using Unity.Mathematics;

/// <summary>Slot-following speed and Walk/Run gear (C9).</summary>
public class SlotFollowSpeedTests
{
    static readonly FormationFollowSpeed.Settings S = FormationFollowSpeed.Settings.From(MovementTuningComponent.Default);
    const float Base = 3f;

    [Test]
    public void SpeedGrowsWithTheDistanceToTheSlot()
    {
        float previous = 0f;
        for (float d = 0f; d < 10f; d += 0.1f)
        {
            float v = FormationFollowSpeed.Speed(Base, 2f, d, MovementGear.Run, S);
            Assert.That(v, Is.GreaterThanOrEqualTo(previous - 1e-5f), $"d={d}");
            previous = v;
        }
    }

    [Test]
    public void SpeedNeverExceedsTheGearTopSpeed()
    {
        foreach (MovementGear gear in new[] { MovementGear.Walk, MovementGear.Run })
            for (float d = 0f; d < 20f; d += 0.5f)
                Assert.That(FormationFollowSpeed.Speed(Base, 10f, d, gear, S),
                    Is.LessThanOrEqualTo(FormationFollowSpeed.MaxSpeed(Base, gear, S) + 1e-5f));
    }

    [Test]
    public void OnTheSlotTheUnitMatchesTheSlotSpeed()
    {
        Assert.That(FormationFollowSpeed.Speed(Base, 2f, 0f, MovementGear.Walk, S), Is.EqualTo(2f).Within(1e-5f));
    }

    [Test]
    public void StillSlotIsApproachedAtTheGearSpeed()
    {
        Assert.That(FormationFollowSpeed.Speed(Base, 0f, 0.3f, MovementGear.Walk, S), Is.EqualTo(Base).Within(1e-5f));
    }

    [Test]
    public void SlowSlotKeepsTheMinimumSpeed()
    {
        Assert.That(FormationFollowSpeed.Speed(Base, FormationFollowSpeed.StillSlotSpeed, 0f, MovementGear.Walk, S),
            Is.EqualTo(Base * S.minSpeedRatio).Within(1e-5f));
    }

    [Test]
    public void NoiseAroundTheThresholdsDoesNotFlipTheGear()
    {
        var gear = MovementGear.Walk;
        int changes = 0;
        // Behind the slot, then noise just inside the hysteresis band around both thresholds.
        float enter = S.runEnterDistance, exit = S.runExitDistance, e = 0.05f;
        float[] distances = { exit - e, enter + e, enter - e, enter + e, enter - 2 * e, (enter + exit) / 2,
                              exit + e, exit + 2 * e, exit - e, exit + e, exit - 2 * e, exit + e };
        foreach (float d in distances)
        {
            var next = FormationFollowSpeed.Gear(gear, d, false, S);
            if (next != gear) changes++;
            gear = next;
        }
        Assert.That(changes, Is.EqualTo(2)); // Walk → Run past the entry, Run → Walk below the exit.
    }

    [Test]
    public void SpeedCapChangesAtMostByTheAccelerationPerFrame()
    {
        Assert.That(FormationFollowSpeed.Approach(3f, 6f, 8f / 60f, 20f / 60f), Is.EqualTo(3f + 8f / 60f).Within(1e-5f));
        Assert.That(FormationFollowSpeed.Approach(6f, 3f, 8f / 60f, 20f / 60f), Is.EqualTo(6f - 20f / 60f).Within(1e-5f));
        Assert.That(FormationFollowSpeed.Approach(3f, 3.05f, 8f / 60f, 20f / 60f), Is.EqualTo(3.05f).Within(1e-5f));
    }

    [Test]
    public void ArrivalCapLetsTheUnitStopWithinTheRemainingDistance()
    {
        const float decel = 12f, dt = 1f / 60f;
        Assert.That(FormationFollowSpeed.ArrivalSpeed(1.5f, decel), Is.EqualTo(6f).Within(1e-4f));
        Assert.That(FormationFollowSpeed.ArrivalSpeed(0f, decel), Is.EqualTo(0f));
        Assert.That(float.IsPositiveInfinity(FormationFollowSpeed.ArrivalSpeed(1f, 0f)), Is.True);

        // Run at 6 m/s towards a still point 3 m away: the capped speed brakes on the way in, no frame drops
        // more than the braking limit and the unit is nearly still when it gets there.
        float speed = 6f, distance = 3f, maxDrop = 0f;
        while (distance > 0.02f)
        {
            float cap = math.min(6f, FormationFollowSpeed.ArrivalSpeed(distance, decel));
            float next = FormationFollowSpeed.Approach(speed, cap, 8f * dt, 20f * dt);
            maxDrop = math.max(maxDrop, (speed - next) / dt);
            speed = next;
            distance -= speed * dt;
        }
        Assert.That(maxDrop, Is.LessThanOrEqualTo(20f + 1e-3f));
        Assert.That(speed, Is.LessThan(1f));
    }

    [Test]
    public void OnlyStillDestinationsAreBrakedInto()
    {
        Assert.That(FormationFollowSpeed.IsStillDestination(true, 3f), Is.False);
        Assert.That(FormationFollowSpeed.IsStillDestination(true, 0.1f), Is.True);
        Assert.That(FormationFollowSpeed.IsStillDestination(false, 3f), Is.True);
    }

    [Test]
    public void ForcedRunWinsAtAnyDistance()
    {
        Assert.That(FormationFollowSpeed.Gear(MovementGear.Walk, 0f, true, S), Is.EqualTo(MovementGear.Run));
    }
}
