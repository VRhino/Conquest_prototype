using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

/// <summary>Movers slide along enemy bodies instead of walking into them (M2, P1).</summary>
public class EnemyContactTests
{
    const float Contact = 1f;
    const float CloseTime = 0.05f;

    [Test]
    public void HeadOnApproachStopsAtTheContactDistance()
    {
        // Touching: no approach left at all.
        Assert.That(EnemyContact.Slide(new float2(0f, 4f), new float2(0f, 1f), Contact, CloseTime).y,
            Is.EqualTo(0f).Within(1e-5f));
        // 0.1 m of gap may close in no less than 0.05 s: at most 2 m/s towards the enemy.
        Assert.That(EnemyContact.Slide(new float2(0f, 4f), new float2(0f, 1.1f), Contact, CloseTime).y,
            Is.EqualTo(2f).Within(1e-4f));
        // Far enough: untouched.
        Assert.That(EnemyContact.Slide(new float2(0f, 4f), new float2(0f, 2f), Contact, CloseTime),
            Is.EqualTo(new float2(0f, 4f)));
    }

    [Test]
    public void SidewaysPartIsKeptAndMovingAwayIsFree()
    {
        // Diagonal into a touching enemy: the sideways part survives, so the unit slides round it.
        var slid = EnemyContact.Slide(new float2(3f, 3f), new float2(0f, 1f), Contact, CloseTime);
        Assert.That(slid.x, Is.EqualTo(3f).Within(1e-5f));
        Assert.That(slid.y, Is.EqualTo(0f).Within(1e-5f));
        // Already overlapping and moving away: never clipped, the bodyblock separates them.
        Assert.That(EnemyContact.Slide(new float2(0f, -2f), new float2(0f, 0.5f), Contact, CloseTime),
            Is.EqualTo(new float2(0f, -2f)));
    }

    [Test]
    public void KnockBackStepStopsAtTheEnemyBody()
    {
        // 0.1 m of gap: a 0.3 m push towards the enemy only closes it (P3).
        Assert.That(EnemyContact.ClipStep(new float2(0f, 0.3f), new float2(0f, 1.1f), Contact).y,
            Is.EqualTo(0.1f).Within(1e-5f));
        // Pushed away from the enemy: untouched.
        Assert.That(EnemyContact.ClipStep(new float2(0f, -0.3f), new float2(0f, 1.1f), Contact),
            Is.EqualTo(new float2(0f, -0.3f)));
    }

    [Test]
    public void OnlySteeredUnitsAreClippedAndOnlyAgainstEnemies()
    {
        var agents = new NativeArray<AllyAvoidance.Agent>(4, Allocator.TempJob);
        var velocities = new NativeArray<float2>(4, Allocator.TempJob);
        var grid = new NativeParallelMultiHashMap<int2, int>(4, Allocator.TempJob);
        try
        {
            // 0: mover of team 0 heading at a touching enemy (1); 2: an ally in its way; 3: a still enemy unit.
            agents[0] = new AllyAvoidance.Agent { position = new float2(0f, 0f), team = 0, steered = true };
            agents[1] = new AllyAvoidance.Agent { position = new float2(0f, 1f), team = 1, steered = false };
            agents[2] = new AllyAvoidance.Agent { position = new float2(1f, 0f), team = 0, steered = false };
            agents[3] = new AllyAvoidance.Agent { position = new float2(0f, 2f), team = 1, steered = false };
            velocities[0] = new float2(2f, 3f);
            velocities[1] = new float2(0f, -3f); // Not steered: kept even though it points at agent 0.
            float cellSize = AllyAvoidance.CellSize(4f);
            for (int i = 0; i < agents.Length; i++) grid.Add((int2)math.floor(agents[i].position / cellSize), i);

            EnemyContact.Run(agents, velocities, grid, cellSize, Contact, CloseTime);

            Assert.That(velocities[0].y, Is.EqualTo(0f).Within(1e-5f), "No approach into the touching enemy.");
            Assert.That(velocities[0].x, Is.EqualTo(2f).Within(1e-5f), "The ally on its side does not clip it.");
            Assert.That(velocities[1], Is.EqualTo(new float2(0f, -3f)), "A unit that is not steered is never touched.");
        }
        finally
        {
            agents.Dispose(); velocities.Dispose(); grid.Dispose();
        }
    }
}
