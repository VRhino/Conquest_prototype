using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>Line of sight per squad and enemy with a ray budget (Aprendizajes_Referencia.md L4-03).</summary>
public class LineOfSightTests
{
    [Test]
    public void APairIsSeenWhileAnyRememberedRayWasClear()
    {
        byte mask = LineOfSight.AllClear;
        for (int i = 0; i < 3; i++) mask = LineOfSight.Record(mask, false);
        Assert.That(LineOfSight.IsVisible(mask, 4), Is.True, "One clear ray still in the last four.");
        mask = LineOfSight.Record(mask, false);
        Assert.That(LineOfSight.IsVisible(mask, 4), Is.False, "Four blocked in a row: hidden.");
        Assert.That(LineOfSight.IsVisible(LineOfSight.Record(mask, true), 4), Is.True, "One clear ray shows it again.");
    }

    [Test]
    public void TheBudgetIsSharedByDemand()
    {
        using var demands = new NativeArray<int>(new[] { 30, 10, 0 }, Allocator.Temp);
        using var quotas = new NativeArray<int>(3, Allocator.Temp);
        LineOfSight.Quotas(demands, 64, quotas);
        Assert.That(quotas.ToArray(), Is.EqualTo(new[] { 30, 10, 0 }), "Everything fits.");
        LineOfSight.Quotas(demands, 9, quotas);
        Assert.That(quotas[0] + quotas[1] + quotas[2], Is.EqualTo(9));
        Assert.That(quotas[0], Is.GreaterThan(quotas[1]), "More pairs, more rays.");
        Assert.That(quotas[2], Is.Zero);
    }

    [Test]
    public void PairsFollowTheEnemiesInRangeAndNewOnesStartVisible()
    {
        using var world = new World("Line of sight sync");
        var em = world.EntityManager;
        var squad = em.CreateEntity();
        var a = em.CreateEntity();
        var b = em.CreateEntity();
        var visibility = em.AddBuffer<SquadVisibilityElement>(squad);
        visibility.Add(new SquadVisibilityElement { enemy = a, clearMask = 0 });
        var inRange = em.AddBuffer<SquadInRangeEnemy>(squad);
        inRange.Add(new SquadInRangeEnemy { Value = b });
        visibility = em.GetBuffer<SquadVisibilityElement>(squad);
        LineOfSight.Sync(visibility, em.GetBuffer<SquadInRangeEnemy>(squad));
        Assert.That(visibility.Length, Is.EqualTo(1));
        Assert.That(visibility[0].enemy, Is.EqualTo(b));
        Assert.That(LineOfSight.Sees(visibility, b, 4), Is.True, "New: visible until a ray says otherwise.");
        Assert.That(LineOfSight.Sees(visibility, a, 4), Is.True, "No pair: not hidden by the rays.");
    }

    [Test]
    public void AWallBetweenTwoSquadsHidesTheEnemy()
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            wall.transform.position = new Vector3(0f, 1.5f, 5f);
            wall.transform.localScale = new Vector3(10f, 4f, 0.5f);
            Physics.SyncTransforms();
            Assert.That(Hidden(new float3(0, 0, 10), 2), Is.True, "Behind the wall.");
            Assert.That(Hidden(new float3(20, 0, 0), 2), Is.False, "Off to the side, in the open.");
        }
        finally { Object.DestroyImmediate(wall); }
    }

    static bool Hidden(float3 enemyPosition, int updates)
    {
        using var world = new World("Line of sight rays");
        var em = world.EntityManager;
        em.AddComponentData(em.CreateEntity(), new SquadSpawnConfigComponent
        {
            lineOfSight = new LineOfSightTuning
            {
                enabled = true, blockingLayers = LineOfSightTuning.DefaultBlockingLayers, raysPerFrame = 8, eyeHeight = 1.5f,
                memory = 2,
            },
        });
        Entity Squad(float3 position, out Entity unit)
        {
            var squad = em.CreateEntity(typeof(SquadVisibilityCursor));
            unit = em.CreateEntity(typeof(LocalTransform));
            em.SetComponentData(unit, LocalTransform.FromPosition(position));
            em.AddBuffer<SquadUnitElement>(squad).Add(new SquadUnitElement { Value = unit });
            em.AddBuffer<SquadVisibilityElement>(squad);
            em.AddBuffer<SquadInRangeEnemy>(squad);
            return squad;
        }
        var observer = Squad(float3.zero, out _);
        var enemy = Squad(enemyPosition, out _);
        em.GetBuffer<SquadInRangeEnemy>(observer).Add(new SquadInRangeEnemy { Value = enemy });
        var system = world.GetOrCreateSystemManaged<LineOfSightSystem>();
        for (int i = 0; i < updates; i++) system.Update();
        return !LineOfSight.Sees(em.GetBuffer<SquadVisibilityElement>(observer), enemy, 2);
    }
}
