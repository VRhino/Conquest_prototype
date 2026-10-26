using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>UnitTargetingSystem with the target score (L4-04, D6) and the squad enemy search (L4-09).</summary>
public class UnitTargetingScoreTests
{
    World world;
    EntityManager em;

    [SetUp]
    public void SetUp()
    {
        world = new World("Unit targeting score");
        em = world.EntityManager;
    }

    [TearDown]
    public void TearDown() => world.Dispose();

    void SetTuning(MovementTuningComponent tuning)
    {
        using var query = em.CreateEntityQuery(typeof(MovementTuningComponent));
        if (query.IsEmpty) em.CreateEntity(typeof(MovementTuningComponent));
        em.SetComponentData(query.GetSingletonEntity(), tuning);
    }

    Entity Enemy(float3 position, Entity owner = default)
    {
        var enemy = em.CreateEntity(typeof(LocalTransform), typeof(UnitOwnerComponent));
        em.SetComponentData(enemy, LocalTransform.FromPosition(position));
        em.SetComponentData(enemy, new UnitOwnerComponent { squad = owner });
        return enemy;
    }

    Entity Unit(float3 position, float attackRange = 0f)
    {
        var unit = attackRange > 0f
            ? em.CreateEntity(typeof(LocalTransform), typeof(UnitCombatComponent), typeof(UnitWeaponComponent))
            : em.CreateEntity(typeof(LocalTransform), typeof(UnitCombatComponent));
        em.SetComponentData(unit, LocalTransform.FromPosition(position));
        if (attackRange > 0f) em.SetComponentData(unit, new UnitWeaponComponent { attackRange = attackRange });
        return unit;
    }

    Entity Squad(Entity[] units, params Entity[] candidates)
    {
        var squad = em.CreateEntity(typeof(SquadAIComponent), typeof(SquadStateComponent));
        em.SetComponentData(squad, new SquadStateComponent { currentState = SquadFSMState.InCombat });
        var buffer = em.AddBuffer<SquadUnitElement>(squad);
        foreach (var unit in units) buffer.Add(new SquadUnitElement { Value = unit });
        var targets = em.AddBuffer<SquadTargetEntity>(squad);
        foreach (var candidate in candidates) targets.Add(new SquadTargetEntity { Value = candidate });
        return squad;
    }

    Entity TargetOf(Entity unit) => em.GetComponentData<UnitCombatComponent>(unit).target;

    void Update() => world.GetOrCreateSystemManaged<UnitTargetingSystem>().Update();

    [Test]
    public void TwoSquadsSpreadOverTwoEnemiesInsteadOfPilingOnTheNearest()
    {
        var near = Enemy(new float3(1.5f, 0f, 0.25f));
        var further = Enemy(new float3(2.5f, 0f, 0.25f));
        var a = new[] { Unit(new float3(0f, 0f, 0f)), Unit(new float3(0.5f, 0f, 0f)) };
        var b = new[] { Unit(new float3(0f, 0f, 0.5f)), Unit(new float3(0.5f, 0f, 0.5f)) };
        Squad(a, near, further);
        Squad(b, near, further);

        Update();

        int onNear = 0, onFurther = 0;
        foreach (var unit in new[] { a[0], a[1], b[0], b[1] })
        {
            if (TargetOf(unit) == near) onNear++;
            else if (TargetOf(unit) == further) onFurther++;
        }
        // Closest-only with a cap of 2 per squad put all four on the nearest enemy.
        Assert.That(onNear, Is.EqualTo(2));
        Assert.That(onFurther, Is.EqualTo(2));
    }

    [Test]
    public void UnitKeepsItsTargetUntilAnotherIsClearlyBetter()
    {
        var tuning = MovementTuningComponent.Default;
        tuning.targetReevaluateInterval = 0f; // Look again every update.
        SetTuning(tuning);
        var current = Enemy(new float3(2f, 0f, 0f));
        var other = Enemy(new float3(-1.8f, 0f, 0f));
        var unit = Unit(float3.zero);
        em.SetComponentData(unit, new UnitCombatComponent { target = current });
        Squad(new[] { unit }, current, other);

        Update();
        Assert.That(TargetOf(unit), Is.EqualTo(current), "A slightly closer enemy is not worth a switch.");

        em.SetComponentData(other, LocalTransform.FromPosition(new float3(-0.3f, 0f, 0f)));
        Update();
        Assert.That(TargetOf(unit), Is.EqualTo(other));
    }

    [Test]
    public void UnitWithinReachKeepsItsCrowdedTarget()
    {
        var tuning = MovementTuningComponent.Default;
        tuning.targetReevaluateInterval = 0f;
        SetTuning(tuning);
        var crowded = Enemy(new float3(1.2f, 0f, 0f));
        var free = Enemy(new float3(-1.5f, 0f, 0f));
        // Three attackers of another squad (outside this test's squads) already on it.
        for (int i = 0; i < 3; i++)
        {
            var other = em.CreateEntity(typeof(UnitCombatComponent));
            em.SetComponentData(other, new UnitCombatComponent { target = crowded });
        }
        var engaged = Unit(float3.zero, attackRange: 1f);
        em.SetComponentData(engaged, new UnitCombatComponent { target = crowded });
        var approaching = Unit(new float3(0f, 0f, 0.1f));
        em.SetComponentData(approaching, new UnitCombatComponent { target = crowded });
        Squad(new[] { engaged, approaching }, crowded, free);

        Update();

        Assert.That(TargetOf(engaged), Is.EqualTo(crowded), "Within reach: no switch.");
        Assert.That(TargetOf(approaching), Is.EqualTo(free), "Not yet engaged: goes to the free enemy.");
    }

    [Test]
    public void SquadLeavesOutEnemiesOnAnotherLevel()
    {
        var onWall = Enemy(new float3(1f, 2.5f, 0f));
        var level = Enemy(new float3(2.5f, 0f, 0f));
        var unit = Unit(float3.zero);
        Squad(new[] { unit }, onWall, level);

        Update();

        Assert.That(TargetOf(unit), Is.EqualTo(level));
    }

    [Test]
    public void CombatRadiusGrowsWhenTheSquadMoves()
    {
        var distant = Enemy(new float3(7f, 0f, 0f));
        var unit = Unit(float3.zero);
        var squad = Squad(new[] { unit }, distant);
        em.AddComponentData(squad, new SquadFormationAnchorComponent { position = float3.zero });

        Update();
        Assert.That(TargetOf(unit), Is.EqualTo(Entity.Null), "Still: 7 m is beyond the smallest radius.");

        em.SetComponentData(squad, new SquadFormationAnchorComponent
        {
            position = float3.zero,
            velocity = new float3(MovementTuningComponent.Default.enemySearchFullRadiusSpeed, 0f, 0f),
        });
        Update();
        Assert.That(TargetOf(unit), Is.EqualTo(distant), "Moving: the radius reaches it.");
    }

    [Test]
    public void HoldingSquadOnlyTakesEnemiesInItsZone()
    {
        var distant = Enemy(new float3(7f, 0f, 0f));
        var unit = Unit(float3.zero);
        var squad = Squad(new[] { unit }, distant);
        em.SetComponentData(squad, new SquadStateComponent
        {
            currentState = SquadFSMState.InCombat, currentOrder = SquadOrderType.HoldPosition,
        });
        em.AddComponentData(squad, new SquadFormationAnchorComponent
        {
            position = float3.zero, velocity = new float3(10f, 0f, 0f),
        });

        Update();

        Assert.That(TargetOf(unit), Is.EqualTo(Entity.Null));
    }

    [Test]
    public void SquadFocusesTheEnemySquadItAlreadyFights()
    {
        var tuning = MovementTuningComponent.Default;
        tuning.targetReevaluateInterval = 0f;
        tuning.targetSaturationPerAttacker = 0f; // Isolate the focus term.
        SetTuning(tuning);
        var enemySquadA = em.CreateEntity();
        var enemySquadB = em.CreateEntity();
        // The squadmate fights enemy squad A further along the line.
        var fought = Enemy(new float3(5.8f, 0f, 0f), enemySquadA);
        var engaged = Unit(new float3(5f, 0f, 0f), attackRange: 1f);
        em.SetComponentData(engaged, new UnitCombatComponent { target = fought });
        // Beside the free unit: one enemy of squad A, one of squad B slightly closer.
        var sameSquad = Enemy(new float3(0f, 0f, 2.1f), enemySquadA);
        var otherSquad = Enemy(new float3(0f, 0f, -2f), enemySquadB);
        var free = Unit(float3.zero);
        Squad(new[] { engaged, free }, fought, sameSquad, otherSquad);

        Update();

        Assert.That(TargetOf(free), Is.EqualTo(sameSquad),
            "Between two enemies at about the same distance, the one of the squad already fought wins.");
    }
}
