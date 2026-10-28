using NUnit.Framework;
using Unity.Entities;
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

    [Test]
    public void PairCollidesOnlyIfBothHaveTheCollisionOn()
    {
        var agents = new NativeArray<AllyAvoidance.Agent>(4, Allocator.TempJob);
        var velocities = new NativeArray<float2>(4, Allocator.TempJob);
        var grid = new NativeParallelMultiHashMap<int2, int>(4, Allocator.TempJob);
        try
        {
            // 0 has the collision off; 2 heads at a touching enemy (3) that has it off. Neither is clipped (P2).
            agents[0] = new AllyAvoidance.Agent { position = new float2(0f, 0f), team = 0, steered = true, passesEnemies = true };
            agents[1] = new AllyAvoidance.Agent { position = new float2(0f, 1f), team = 1 };
            agents[2] = new AllyAvoidance.Agent { position = new float2(10f, 0f), team = 0, steered = true };
            agents[3] = new AllyAvoidance.Agent { position = new float2(10f, 1f), team = 1, passesEnemies = true };
            velocities[0] = new float2(0f, 3f);
            velocities[2] = new float2(0f, 3f);
            float cellSize = AllyAvoidance.CellSize(4f);
            for (int i = 0; i < agents.Length; i++) grid.Add((int2)math.floor(agents[i].position / cellSize), i);

            EnemyContact.Run(agents, velocities, grid, cellSize, Contact, CloseTime);

            Assert.That(velocities[0], Is.EqualTo(new float2(0f, 3f)), "The mover has the collision off.");
            Assert.That(velocities[2], Is.EqualTo(new float2(0f, 3f)), "The enemy has the collision off.");
        }
        finally
        {
            agents.Dispose(); velocities.Dispose(); grid.Dispose();
        }
    }

    [Test]
    public void BlockerIsTheNearestEnemyInFrontThatClips()
    {
        var preferred = new float2(0f, 3f);
        Assert.That(EnemyContact.Blocks(preferred, preferred, new float2(0f, 1f), Contact, CloseTime), Is.True);
        Assert.That(EnemyContact.Blocks(preferred, preferred, new float2(0.8f, 0.6f), Contact, CloseTime), Is.False,
            "53° off the way: it clips, but it is beside the unit, not in front.");
        Assert.That(EnemyContact.Blocks(preferred, preferred, new float2(0f, 3f), Contact, CloseTime), Is.False, "Too far to clip.");

        var agents = new NativeArray<AllyAvoidance.Agent>(3, Allocator.TempJob);
        var velocities = new NativeArray<float2>(3, Allocator.TempJob);
        var blockers = new NativeArray<int>(3, Allocator.TempJob);
        var grid = new NativeParallelMultiHashMap<int2, int>(3, Allocator.TempJob);
        try
        {
            agents[0] = new AllyAvoidance.Agent { position = new float2(0f, 0f), team = 0, steered = true, preferred = preferred };
            agents[1] = new AllyAvoidance.Agent { position = new float2(0.1f, 1.05f), team = 1 };
            agents[2] = new AllyAvoidance.Agent { position = new float2(0f, 1f), team = 1 };
            velocities[0] = preferred;
            float cellSize = AllyAvoidance.CellSize(4f);
            for (int i = 0; i < agents.Length; i++) grid.Add((int2)math.floor(agents[i].position / cellSize), i);

            EnemyContact.Run(agents, velocities, grid, cellSize, Contact, CloseTime, blockers);

            Assert.That(blockers[0], Is.EqualTo(2), "The nearest of the two in front.");
            Assert.That(blockers[1], Is.EqualTo(-1), "Not steered: never blocked.");
        }
        finally
        {
            agents.Dispose(); velocities.Dispose(); blockers.Dispose(); grid.Dispose();
        }
    }

    [Test]
    public void SquadSwitchAndHeavyHitTurnTheCollisionOff()
    {
        using var world = new World("Enemy collision");
        var em = world.EntityManager;
        var squad = em.CreateEntity();
        var unit = em.CreateEntity(typeof(UnitHitKnockbackComponent));
        em.AddComponentData(unit, new UnitOwnerComponent { squad = squad });
        var hero = em.CreateEntity();

        Assert.That(EnemyCollision.PassesEnemies(em, unit), Is.False, "Without the component the squad collides.");
        Assert.That(EnemyCollision.PassesEnemies(em, hero), Is.False, "Bodies without squad always collide.");
        em.AddComponentData(squad, new SquadEnemyCollisionComponent { enabled = false });
        Assert.That(EnemyCollision.PassesEnemies(em, unit), Is.True);
        em.SetComponentData(squad, new SquadEnemyCollisionComponent { enabled = true });
        Assert.That(EnemyCollision.PassesEnemies(em, unit), Is.False);
        em.SetComponentData(unit, new UnitHitKnockbackComponent { active = true, passesEnemies = true });
        Assert.That(EnemyCollision.PassesEnemies(em, unit), Is.True, "A heavy hit lets it through while the push lasts.");
        em.SetComponentData(unit, new UnitHitKnockbackComponent { active = false, passesEnemies = true });
        Assert.That(EnemyCollision.PassesEnemies(em, unit), Is.False, "Over when the push ends.");
    }
}
