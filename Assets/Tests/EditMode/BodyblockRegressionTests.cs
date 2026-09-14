using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class BodyblockRegressionTests
{
    [TestCase(30)] [TestCase(60)] [TestCase(144)]
    public void PushClampRepresentsTheSameSpeedAtEveryFrameRate(int fps)
    {
        float dt = 1f / fps;
        Vector3 result = UnitBodyblockSystem.ClampOffsetPerSecond(Vector3.right * 100, 18, dt);
        Assert.That(result.magnitude / dt, Is.EqualTo(18).Within(0.0001f));
    }

    [Test]
    public void ExactOverlapGetsAStableFiniteDirection()
    {
        using var world = new World("Bodyblock direction");
        Entity first = world.EntityManager.CreateEntity();
        Entity second = world.EntityManager.CreateEntity();
        float3 direction = UnitBodyblockSystem.GetOverlapDirection(first, second);
        Assert.That(math.all(math.isfinite(direction)), Is.True);
        Assert.That(math.length(direction), Is.EqualTo(1).Within(0.0001f));
        Assert.That(UnitBodyblockSystem.GetOverlapDirection(first, second), Is.EqualTo(direction));
    }
}
