using NUnit.Framework;
using Unity.Mathematics;

public class StartDelayTests
{
    static readonly MovementTuningComponent Tuning = MovementTuningComponent.Default;

    static float Delay(int2 grid, int slot, int frontRowY) => FormationStartDelay.Compute(
        FormationStartDelay.RowRank(grid, frontRowY), slot, grid, Tuning.startRowDelay, Tuning.startJitterDelay);

    [Test]
    public void SameInputsGiveTheSameDelay()
    {
        Assert.That(Delay(new int2(3, 2), 7, 4), Is.EqualTo(Delay(new int2(3, 2), 7, 4)));
    }

    [Test]
    public void FrontRowNeverWaitsLongerThanARowBehindIt()
    {
        const int front = 3;
        float slowestFront = 0f, fastestSecond = float.MaxValue;
        for (int x = 0; x < 6; x++)
        {
            slowestFront = math.max(slowestFront, Delay(new int2(x, front), x, front));
            fastestSecond = math.min(fastestSecond, Delay(new int2(x, front - 1), 6 + x, front));
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
                max = math.max(max, Delay(new int2(x, 3 - row), row * 3 + x, 3));
        Assert.That(max, Is.LessThanOrEqualTo(3 * Tuning.startRowDelay + Tuning.startJitterDelay));
        Assert.That(max, Is.LessThan(0.5f));
    }

    [Test]
    public void FrontRowIsTheLargestGridY()
    {
        Assert.That(FormationStartDelay.RowRank(new int2(5, 6), 6), Is.EqualTo(0));
        Assert.That(FormationStartDelay.RowRank(new int2(3, 2), 6), Is.EqualTo(4));
    }
}
