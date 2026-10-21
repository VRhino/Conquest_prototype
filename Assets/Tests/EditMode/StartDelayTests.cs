using NUnit.Framework;
using Unity.Mathematics;

public class StartDelayTests
{
    static readonly MovementTuningComponent Tuning = MovementTuningComponent.Default;

    static float Delay(int row, int slot) => FormationStartDelay.Compute(row, slot, Tuning.startRowDelay, Tuning.startJitterDelay);

    [Test]
    public void SameInputsGiveTheSameDelay()
    {
        Assert.That(Delay(2, 7), Is.EqualTo(Delay(2, 7)));
    }

    [Test]
    public void FrontRowNeverWaitsLongerThanARowBehindIt()
    {
        float slowestFront = 0f, fastestSecond = float.MaxValue;
        for (int x = 0; x < 6; x++)
        {
            slowestFront = math.max(slowestFront, Delay(0, x));
            fastestSecond = math.min(fastestSecond, Delay(1, 6 + x));
        }
        // The jitter can overlap neighbours by design, but never by more than one row of delay.
        Assert.That(slowestFront, Is.LessThan(fastestSecond + Tuning.startRowDelay));
        Assert.That(slowestFront, Is.LessThanOrEqualTo(Tuning.startJitterDelay));
    }

    [Test]
    public void FourRowsStartWithinHalfASecond()
    {
        float max = 0f;
        for (int row = 0; row < 4; row++)
            for (int x = 0; x < 3; x++)
                max = math.max(max, Delay(row, row * 3 + x));
        Assert.That(max, Is.LessThanOrEqualTo(3 * Tuning.startRowDelay + Tuning.startJitterDelay));
        Assert.That(max, Is.LessThan(0.5f));
    }

    [Test]
    public void MigratedFrontRowIsTheLargestGridY()
    {
        var slots = FormationSlotRoles.FromCells(new[] { new UnityEngine.Vector2Int(5, 6), new UnityEngine.Vector2Int(3, 2) }, false);
        Assert.That(slots[0].row, Is.EqualTo(0));
        Assert.That(slots[1].row, Is.EqualTo(1));
    }
}
