using ConquestTactics.Animation;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;

/// <summary>Squad stamina spent by running and recovered by walking (Aprendizajes_Referencia.md L5-7).</summary>
public class SquadStaminaTests
{
    static SquadStaminaComponent Pool() => new SquadStaminaComponent
    {
        current = 10f, max = 10f, runCostPerSoldier = 1f, recoveryPerSecond = 2f, recoverFraction = 0.5f,
    };

    [Test]
    public void RunnersSpendWalkersRecoverAndAnEmptyPoolWaitsForTheThreshold()
    {
        var s = Pool();
        SquadStamina.Step(ref s, 4, 1f);
        Assert.That(s.current, Is.EqualTo(6f), "Four runners for a second.");
        SquadStamina.Step(ref s, 10, 1f);
        Assert.That(s.current, Is.EqualTo(0f));
        Assert.That(s.exhausted, Is.True);
        SquadStamina.Step(ref s, 0, 2f);
        Assert.That(s.current, Is.EqualTo(4f));
        Assert.That(s.exhausted, Is.True, "Below half: still cannot run.");
        SquadStamina.Step(ref s, 0, 1f);
        Assert.That(s.exhausted, Is.False, "Half recovered: may run again.");
        SquadStamina.Step(ref s, 0, 10f);
        Assert.That(s.current, Is.EqualTo(10f), "Never above the maximum.");
    }

    [Test]
    public void OnlyARunFasterThanWalkingCounts()
    {
        Assert.That(SquadStamina.IsRunning(MovementGear.Run, 6f, 3f, 1.1f), Is.True);
        Assert.That(SquadStamina.IsRunning(MovementGear.Run, 0f, 3f, 1.1f), Is.False, "Fighting standing in the Run gear.");
        Assert.That(SquadStamina.IsRunning(MovementGear.Run, 3f, 3f, 1.1f), Is.False, "At walking pace.");
        Assert.That(SquadStamina.IsRunning(MovementGear.Walk, 6f, 3f, 1.1f), Is.False);
    }

    [Test]
    public void AZeroMaximumMeansNoStamina()
    {
        var s = new SquadStaminaComponent { runCostPerSoldier = 1f };
        SquadStamina.Step(ref s, 30, 5f);
        Assert.That(s.exhausted, Is.False);
    }

    [Test]
    public void TheCostScalesWithTheSquadStack()
    {
        var s = Pool();
        SquadStamina.Step(ref s, 4, 1f, 0.5f);
        Assert.That(s.current, Is.EqualTo(8f), "Four runners at half the cost.");
        SquadStamina.Step(ref s, 4, 1f, -1f);
        Assert.That(s.current, Is.EqualTo(8f), "A negative scale is free running: no cost and no recovery.");
    }

    [TestCase(1f, 9f)]
    [TestCase(0.5f, 9.5f)]
    public void TheSystemCountsTheSoldiersThatReallyRun(float costScale, float expected)
    {
        using var world = new World("Squad stamina");
        var em = world.EntityManager;
        var squad = em.CreateEntity();
        em.AddComponentData(squad, Pool());
        if (costScale != 1f)
        {
            var stats = SquadAIStatsComponent.Neutral;
            stats.staminaCostScale = costScale;
            em.AddComponentData(squad, stats);
        }
        em.AddBuffer<SquadUnitElement>(squad);
        // Two run at 6 m/s, one fights standing in the Run gear, one walks.
        float[] speeds = { 6f, 6f, 0f, 3f };
        for (int i = 0; i < speeds.Length; i++)
        {
            var unit = em.CreateEntity();
            em.AddComponentData(unit, new UnitMovementGearComponent { gear = i < 3 ? MovementGear.Run : MovementGear.Walk });
            em.AddComponentData(unit, new UnitAnimationMovementComponent { CurrentSpeed = speeds[i], MaxSpeed = 3f });
            em.GetBuffer<SquadUnitElement>(squad).Add(new SquadUnitElement { Value = unit });
        }
        world.SetTime(new TimeData(1.0, 0.5f));
        var system = world.GetOrCreateSystem<SquadStaminaSystem>();
        system.Update(world.Unmanaged);
        Assert.That(em.GetComponentData<SquadStaminaComponent>(squad).current, Is.EqualTo(expected).Within(1e-5f),
            "Two runners, half a second, at the squad's cost scale.");
    }
}
