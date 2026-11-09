using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// The squires' Testudo Advance (<see cref="SquadAdvanceSystem"/>): it only starts holding position in Testudo with every
/// soldier formed, walks the hold centre straight ahead at a fraction of the soldiers' speed, and stops on another toggle,
/// another order or formation, or an enemy within contact distance, keeping the ground it took.
/// </summary>
public class SquadAdvanceTests
{
    const float SoldierSpeed = 3f;
    const float Step = 0.5f;
    const float Spacing = 0.8f;

    World _world;
    EntityManager _em;
    Entity _squad;
    Entity[] _units;
    double _elapsed;

    static float Expected => MovementTuningComponent.Default.advanceSpeedRatio * SoldierSpeed;

    [SetUp]
    public void SetUp()
    {
        _world = new World("Testudo advance");
        _em = _world.EntityManager;
        _em.CreateEntity(typeof(MatchStateComponent));
        _squad = _em.CreateEntity(typeof(SquadStateComponent), typeof(FormationComponent), typeof(SquadAdvanceComponent),
            typeof(SquadAdvanceToggleRequest));
        _em.SetComponentEnabled<SquadAdvanceComponent>(_squad, false);
        _em.SetComponentEnabled<SquadAdvanceToggleRequest>(_squad, false);
        _em.SetComponentData(_squad, new SquadStateComponent { currentOrder = SquadOrderType.HoldPosition });
        _em.SetComponentData(_squad, new FormationComponent { currentFormation = FormationType.Testudo, spacing = Spacing });
        _em.AddComponentData(_squad, new SquadHoldPositionComponent { holdCenter = new float3(5, 0, 5), holdRotation = quaternion.identity });
        _units = new Entity[3];
        var buffer = _em.AddBuffer<SquadUnitElement>(_squad);
        for (int i = 0; i < _units.Length; i++)
        {
            _units[i] = _em.CreateEntity(typeof(UnitStatsComponent));
            _em.SetComponentData(_units[i], new UnitStatsComponent { speed = SoldierSpeed });
            _em.AddComponentData(_units[i], new UnitFormationStateComponent { State = UnitFormationState.Formed });
            _em.AddComponentData(_units[i], LocalTransform.FromPosition(new float3(5 + i, 0, 5)));
            _em.AddComponentData(_units[i], new UnitResolvedSlotComponent { position = new float3(5 + i, 0, 5) });
            _em.AddComponentData(_units[i], new UnitGridSlotComponent { role = FormationSlotRole.Front });
            _em.GetBuffer<SquadUnitElement>(_squad).Add(new SquadUnitElement { Value = _units[i] });
        }
        _elapsed = 0;
    }
    [TearDown] public void TearDown() => _world.Dispose();

    void Update(float dt = Step)
    {
        _elapsed += dt;
        _world.SetTime(new TimeData(_elapsed, dt));
        _world.GetOrCreateSystemManaged<SquadAdvanceSystem>().Update();
    }
    void Toggle()
    {
        _em.SetComponentEnabled<SquadAdvanceToggleRequest>(_squad, true);
        Update();
    }
    bool Advancing => _em.IsComponentEnabled<SquadAdvanceComponent>(_squad);
    float3 Offset => _em.GetComponentData<SquadHoldPositionComponent>(_squad).advanceOffset;

    [Test]
    public void StartsOnlyHoldingATestudoWithEverySoldierFormed()
    {
        _em.SetComponentData(_squad, new SquadStateComponent { currentOrder = SquadOrderType.FollowHero });
        Toggle();
        Assert.That(Advancing, Is.False, "not while following");
        _em.SetComponentData(_squad, new SquadStateComponent { currentOrder = SquadOrderType.HoldPosition });
        _em.SetComponentData(_squad, new FormationComponent { currentFormation = FormationType.ShieldWall });
        Toggle();
        Assert.That(Advancing, Is.False, "not in another formation");
        _em.SetComponentData(_squad, new FormationComponent { currentFormation = FormationType.Testudo, spacing = Spacing });
        _em.SetComponentData(_units[1], new UnitFormationStateComponent { State = UnitFormationState.Moving });
        _em.SetComponentData(_units[1], new UnitResolvedSlotComponent { position = new float3(6, 0, 5 + 2 * Spacing) });
        Toggle();
        Assert.That(Advancing, Is.False, "not with a soldier more than a slot away from its own");
        Assert.That(_em.IsComponentEnabled<SquadAdvanceToggleRequest>(_squad), Is.False, "a refused toggle is consumed");
        // A tight testudo can leave a soldier just short of its slot: within one spacing counts as in position.
        _em.SetComponentData(_units[1], new UnitResolvedSlotComponent { position = new float3(6, 0, 5 + 0.5f * Spacing) });
        Toggle();
        Assert.That(Advancing, Is.True);
        var advance = _em.GetComponentData<SquadAdvanceComponent>(_squad);
        Assert.That(advance.speed, Is.EqualTo(Expected).Within(1e-5f));
        Assert.That(math.distance(advance.direction, math.forward()), Is.LessThan(1e-5f), "along the formation's facing");
    }

    [Test]
    public void WalksStraightAheadUntilToggledAgainAndKeepsTheGround()
    {
        Toggle();
        Update();
        Update();
        Assert.That(Offset.z, Is.EqualTo(2 * Step * Expected).Within(1e-4f));
        Assert.That(Offset.x, Is.EqualTo(0f).Within(1e-5f));
        Toggle();
        Assert.That(Advancing, Is.False);
        float3 reached = Offset;
        Update();
        Assert.That(Offset, Is.EqualTo(reached), "the squad holds where it stopped");
        Toggle();
        Update();
        Assert.That(Offset.z, Is.GreaterThan(reached.z), "and can advance again from there");
    }

    [Test]
    public void AnotherOrderOrFormationStopsIt()
    {
        Toggle();
        _em.SetComponentData(_squad, new FormationComponent { currentFormation = FormationType.ShieldWall });
        Update();
        Assert.That(Advancing, Is.False, "formation change");

        _em.SetComponentData(_squad, new FormationComponent { currentFormation = FormationType.Testudo });
        Toggle();
        var hold = _em.GetComponentData<SquadHoldPositionComponent>(_squad);
        hold.holdCenter = new float3(20, 0, 20);
        _em.SetComponentData(_squad, hold);
        Update();
        Assert.That(Advancing, Is.False, "a new Hold Position order");

        Toggle();
        _em.SetComponentData(_squad, new SquadStateComponent { currentOrder = SquadOrderType.FollowHero });
        Update();
        Assert.That(Advancing, Is.False, "follow");
    }

    [Test]
    public void TouchingAnEnemyStopsItAndHoldsThere()
    {
        Toggle();
        Update();
        var enemy = _em.CreateEntity();
        float reach = MovementTuningComponent.Default.advanceContactDistance;
        _em.AddComponentData(enemy, LocalTransform.FromPosition(new float3(5, 0, 5 + reach * 0.5f)));
        _em.AddBuffer<SquadTargetEntity>(_squad).Add(new SquadTargetEntity { Value = enemy });
        float3 reached = Offset;
        Update();
        Assert.That(Advancing, Is.False);
        Assert.That(Offset, Is.EqualTo(reached));
        Toggle();
        Assert.That(Advancing, Is.False, "it does not start against an enemy");
    }
}
