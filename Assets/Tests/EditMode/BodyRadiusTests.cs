using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

/// <summary>Body radius per unit type, used per pair (Aprendizajes_Referencia.md L5-5).</summary>
public class BodyRadiusTests
{
    const float DefaultPair = 1f; // Two default bodies of 0.5 m.

    [Test]
    public void ADeclaredRadiusWinsAndZeroMeansTheGlobalOne()
    {
        Assert.That(BodyRadius.Resolve(0f, 0.5f), Is.EqualTo(0.5f));
        Assert.That(BodyRadius.Resolve(1.2f, 0.5f), Is.EqualTo(1.2f));
        Assert.That(AllyAvoidance.RadiusOf(new AllyAvoidance.Agent(), DefaultPair), Is.EqualTo(0.5f),
            "An agent without a radius keeps half the default pair, so older callers are unchanged.");
        Assert.That(AllyAvoidance.RadiusOf(new AllyAvoidance.Agent { radius = 1.2f }, DefaultPair), Is.EqualTo(1.2f));
    }

    [Test]
    public void ALargeBodyStopsAnEnemyFurtherAway()
    {
        Assert.That(ApproachLeft(0f), Is.EqualTo(0.2f).Within(1e-4f), "Two default bodies touch at 1 m: 0.2 m left to close.");
        Assert.That(ApproachLeft(1.5f), Is.EqualTo(0f).Within(1e-4f),
            "A 1.5 m body touches at 2 m: the mover at 1.2 m may not come any closer.");
    }

    /// <summary>Speed towards a still enemy 1.2 m ahead left after enemy contact, for an enemy of <paramref name="enemyRadius"/>.</summary>
    static float ApproachLeft(float enemyRadius)
    {
        var agents = new NativeArray<AllyAvoidance.Agent>(2, Allocator.TempJob);
        var velocities = new NativeArray<float2>(2, Allocator.TempJob);
        var grid = new NativeParallelMultiHashMap<int2, int>(2, Allocator.TempJob);
        try
        {
            agents[0] = new AllyAvoidance.Agent { position = float2.zero, team = 0, steered = true, preferred = new float2(0f, 3f) };
            agents[1] = new AllyAvoidance.Agent { position = new float2(0f, 1.2f), team = 1, radius = enemyRadius };
            velocities[0] = new float2(0f, 3f);
            float cellSize = AllyAvoidance.CellSize(4f);
            for (int i = 0; i < agents.Length; i++) grid.Add((int2)math.floor(agents[i].position / cellSize), i);
            // closeTime 1 s: the allowed approach is the gap left to the contact distance.
            EnemyContact.Run(agents, velocities, grid, cellSize, DefaultPair, 1f);
            return velocities[0].y;
        }
        finally
        {
            agents.Dispose(); velocities.Dispose(); grid.Dispose();
        }
    }
}
