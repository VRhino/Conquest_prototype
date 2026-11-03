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
    public void AZeroMaximumMeansNoStamina()
    {
        var s = new SquadStaminaComponent { runCostPerSoldier = 1f };
        SquadStamina.Step(ref s, 30, 5f);
        Assert.That(s.exhausted, Is.False);
    }

    [Test]
    public void TheSystemCountsTheSoldiersInTheRunGear()
    {
        using var world = new World("Squad stamina");
        var em = world.EntityManager;
        var squad = em.CreateEntity();
        em.AddComponentData(squad, Pool());
        em.AddBuffer<SquadUnitElement>(squad);
        for (int i = 0; i < 3; i++)
        {
            var unit = em.CreateEntity();
            em.AddComponentData(unit, new UnitMovementGearComponent { gear = i < 2 ? MovementGear.Run : MovementGear.Walk });
            em.GetBuffer<SquadUnitElement>(squad).Add(new SquadUnitElement { Value = unit });
        }
        world.SetTime(new TimeData(1.0, 0.5f));
        var system = world.GetOrCreateSystem<SquadStaminaSystem>();
        system.Update(world.Unmanaged);
        Assert.That(em.GetComponentData<SquadStaminaComponent>(squad).current, Is.EqualTo(9f).Within(1e-5f),
            "Two runners, half a second.");
    }
}
