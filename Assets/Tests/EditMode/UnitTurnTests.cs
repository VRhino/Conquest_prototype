using NUnit.Framework;
using Unity.Mathematics;

public class UnitTurnTests
{
    const float Dt = 1f / 60f;
    const float Rate = 450f;

    [Test]
    public void NeverTurnsMoreThanTheRatePerFrame()
    {
        var current = quaternion.identity;
        var target = quaternion.RotateY(math.PI); // Half turn.
        var next = UnitTurn.Step(current, target, Rate, Dt);
        Assert.That(math.degrees(UnitTurn.Angle(current, next)), Is.EqualTo(Rate * Dt).Within(0.01f));
    }

    [Test]
    public void ReachesTheTargetWhenLessThanOneStepRemains()
    {
        var target = quaternion.RotateY(math.radians(3f));
        var next = UnitTurn.Step(quaternion.identity, target, Rate, Dt); // 7.5° per frame available.
        Assert.That(UnitTurn.Angle(next, target), Is.LessThan(1e-4f));
    }

    [Test]
    public void HalfTurnTakesAboutFourTenthsOfASecond()
    {
        var current = quaternion.identity;
        var target = quaternion.RotateY(math.PI);
        int frames = 0;
        while (UnitTurn.Angle(current, target) > 1e-4f && frames < 1000)
        {
            current = UnitTurn.Step(current, target, Rate, Dt);
            frames++;
        }
        Assert.That(frames * Dt, Is.EqualTo(180f / Rate).Within(2 * Dt));
    }

    [Test]
    public void TurnsTheSameAmountInBothDirections()
    {
        var left = UnitTurn.Step(quaternion.identity, quaternion.RotateY(math.radians(90f)), Rate, Dt);
        var right = UnitTurn.Step(quaternion.identity, quaternion.RotateY(math.radians(-90f)), Rate, Dt);
        Assert.That(UnitTurn.Angle(quaternion.identity, left),
            Is.EqualTo(UnitTurn.Angle(quaternion.identity, right)).Within(1e-5f));
    }
}
