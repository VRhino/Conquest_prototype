using NUnit.Framework;

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
    public void ForcedRunWinsAtAnyDistance()
    {
        Assert.That(FormationFollowSpeed.Gear(MovementGear.Walk, 0f, true, S), Is.EqualTo(MovementGear.Run));
    }
}
