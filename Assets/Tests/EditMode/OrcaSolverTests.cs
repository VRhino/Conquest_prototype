using System.Diagnostics;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

/// <summary>ORCA half-planes and the allied avoidance built on them (C14).</summary>
public class OrcaSolverTests
{
    static readonly AllyAvoidance.Settings Settings = new AllyAvoidance.Settings
    {
        neighborRadius = 4f, maxNeighbors = 8, combinedRadius = 1f, squadmateCombinedRadius = 0.9f,
        collisionResolveTime = 0.25f,
    };

    static AllyAvoidance.Agent Walker(float2 position, float2 preferred, int team = 0, bool steered = true) =>
        new AllyAvoidance.Agent
        {
            position = position, velocity = preferred, preferred = preferred, maxSpeed = 3f,
            timeHorizon = 2f, team = team, squad = -1, steered = steered,
        };

    /// <summary>Moves the agents with the avoiding velocity; returns the smallest distance between agents 0 and 1.</summary>
    static float Simulate(AllyAvoidance.Agent[] start, float seconds, out AllyAvoidance.Agent[] end)
    {
        const float dt = 0.02f;
        var agents = new NativeArray<AllyAvoidance.Agent>(start, Allocator.TempJob);
        var velocities = new NativeArray<float2>(start.Length, Allocator.TempJob);
        try
        {
            float minDistance = float.MaxValue;
            for (float t = 0f; t < seconds; t += dt)
            {
                AllyAvoidance.Run(agents, velocities, Settings);
                for (int i = 0; i < agents.Length; i++)
                {
                    var a = agents[i];
                    a.velocity = velocities[i];
                    a.position += a.velocity * dt;
                    agents[i] = a;
                }
                minDistance = math.min(minDistance, math.distance(agents[0].position, agents[1].position));
            }
            end = agents.ToArray();
            return minDistance;
        }
        finally
        {
            agents.Dispose();
            velocities.Dispose();
        }
    }

    [Test]
    public void WithoutNeighboursTheVelocityIsThePreferredOne()
    {
        var lines = new FixedList512Bytes<float4>();
        Assert.That(OrcaSolver.Solve(ref lines, new float2(1f, 2f), 3f), Is.EqualTo(new float2(1f, 2f)));
        float2 clamped = OrcaSolver.Solve(ref lines, new float2(10f, 0f), 3f);
        Assert.That(math.length(clamped), Is.EqualTo(3f).Within(1e-4f));
    }

    [Test]
    public void HeadOnAlliesPassWithoutTouchingAndKeepGoing()
    {
        float min = Simulate(new[]
        {
            Walker(new float2(-5f, 0.02f), new float2(1.5f, 0f)),
            Walker(new float2(5f, 0f), new float2(-1.5f, 0f)),
        }, 8f, out var end);
        Assert.That(min, Is.GreaterThan(0.95f));
        Assert.That(end[0].position.x, Is.GreaterThan(3f));
        Assert.That(end[1].position.x, Is.LessThan(-3f));
    }

    [Test]
    public void StillAllyIsAvoidedByTheMoverAlone()
    {
        float min = Simulate(new[]
        {
            Walker(new float2(-5f, 0.05f), new float2(1.5f, 0f)),
            Walker(new float2(0f, 0f), float2.zero, steered: false),
        }, 8f, out var end);
        Assert.That(min, Is.GreaterThan(0.95f));
        Assert.That(end[1].position, Is.EqualTo(float2.zero));
        Assert.That(end[0].position.x, Is.GreaterThan(3f));
    }

    [Test]
    public void FormedSquadmatesAreCrossedAndMovingOnesKeepTheSquadmateRadius()
    {
        // Bodies 0.95 m ahead: inside the full combined radius, outside the squadmate one.
        var self = Walker(new float2(0f, 0f), float2.zero);
        self.squad = 1;
        var formedSquadmate = Walker(new float2(0.5f, 0f), float2.zero, steered: false);
        formedSquadmate.squad = 1;
        var movingSquadmate = Walker(new float2(0.95f, 0f), float2.zero);
        movingSquadmate.squad = 1;
        var otherSquad = Walker(new float2(0.95f, 0f), float2.zero, steered: false);
        otherSquad.squad = 2;
        foreach (var (other, pushed) in new[] { (formedSquadmate, false), (movingSquadmate, false), (otherSquad, true) })
        {
            using var agents = new NativeArray<AllyAvoidance.Agent>(new[] { self, other }, Allocator.TempJob);
            using var velocities = new NativeArray<float2>(2, Allocator.TempJob);
            AllyAvoidance.Run(agents, velocities, Settings);
            Assert.That(velocities[0].x < -0.01f, Is.EqualTo(pushed), $"squad {other.squad}, steered {other.steered}");
        }
    }

    [Test]
    public void EnemiesAreNotAvoided()
    {
        using var agents = new NativeArray<AllyAvoidance.Agent>(new[]
        {
            Walker(new float2(-1f, 0f), new float2(1.5f, 0f), team: 0),
            Walker(new float2(1f, 0f), new float2(-1.5f, 0f), team: 1),
        }, Allocator.TempJob);
        using var velocities = new NativeArray<float2>(2, Allocator.TempJob);
        AllyAvoidance.Run(agents, velocities, Settings);
        Assert.That(velocities[0], Is.EqualTo(new float2(1.5f, 0f)));
    }

    [Test]
    public void AvoidingVelocityNeverExceedsTheMaximumSpeed()
    {
        var crowd = new AllyAvoidance.Agent[25];
        for (int i = 0; i < crowd.Length; i++)
            crowd[i] = Walker(new float2(i % 5, i / 5) * 1.1f, math.normalizesafe(new float2(2f - i % 5, 2f - i / 5)) * 3f);
        using var agents = new NativeArray<AllyAvoidance.Agent>(crowd, Allocator.TempJob);
        using var velocities = new NativeArray<float2>(crowd.Length, Allocator.TempJob);
        AllyAvoidance.Run(agents, velocities, Settings);
        foreach (float2 v in velocities)
            Assert.That(math.length(v), Is.LessThanOrEqualTo(3f + 1e-3f));
    }

    [Test, Explicit("Benchmark (D8): 900 agents must solve in at most 1 ms.")]
    public void NineHundredAgentsSolveWithinOneMillisecond()
    {
        var crowd = new AllyAvoidance.Agent[900];
        var random = new Random(7);
        for (int i = 0; i < crowd.Length; i++)
            crowd[i] = Walker(new float2(i % 30, i / 30), random.NextFloat2Direction() * 3f, team: i % 2);
        using var agents = new NativeArray<AllyAvoidance.Agent>(crowd, Allocator.TempJob);
        using var velocities = new NativeArray<float2>(crowd.Length, Allocator.TempJob);
        for (int i = 0; i < 5; i++) AllyAvoidance.Run(agents, velocities, Settings); // Warm-up and Burst compile.

        var samples = new double[21];
        for (int i = 0; i < samples.Length; i++)
        {
            var watch = Stopwatch.StartNew();
            AllyAvoidance.Run(agents, velocities, Settings);
            samples[i] = watch.Elapsed.TotalMilliseconds;
        }
        System.Array.Sort(samples);
        UnityEngine.Debug.Log($"[OrcaBenchmark] 900 agents: median {samples[10]:F3} ms, max {samples[20]:F3} ms");
        Assert.That(samples[10], Is.LessThanOrEqualTo(1.0));
    }
}
