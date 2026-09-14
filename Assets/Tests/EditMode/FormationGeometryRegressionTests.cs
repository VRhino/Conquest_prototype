using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public class FormationGeometryRegressionTests
{
    static BlobAssetReference<FormationLibraryBlob> CreateFormation(params int2[] points)
    {
        using var builder = new BlobBuilder(Allocator.Temp);
        ref var root = ref builder.ConstructRoot<FormationLibraryBlob>();
        var formations = builder.Allocate(ref root.formations, 1);
        formations[0].formationType = FormationType.Line;
        var grid = builder.Allocate(ref formations[0].gridPositions, points.Length);
        for (int i = 0; i < points.Length; i++) grid[i] = points[i];
        return builder.CreateBlobAssetReference<FormationLibraryBlob>(Allocator.Persistent);
    }

    [Test]
    public void EvenFormationHasFractionalUnbiasedCenter()
    {
        var blob = CreateFormation(new int2(0, 0), new int2(1, 0));
        try
        {
            ref var grid = ref blob.Value.formations[0].gridPositions;
            float2 center = FormationPositionCalculator.CalculateFormationCenter(ref grid);
            Assert.That(center, Is.EqualTo(new float2(0.5f, 0)));
            var state = new SquadStateComponent();
            FormationPositionCalculator.CalculateDesiredPosition(Entity.Null, ref grid, 0, center,
                state, null, float3.zero, out _, out var left, out _, false);
            FormationPositionCalculator.CalculateDesiredPosition(Entity.Null, ref grid, 1, center,
                state, null, float3.zero, out _, out var right, out _, false);
            Assert.That(left.x, Is.EqualTo(-0.5f));
            Assert.That(right.x, Is.EqualTo(0.5f));
            Assert.That((left + right) * 0.5f, Is.EqualTo(float3.zero));
        }
        finally { blob.Dispose(); }
    }

    [Test]
    public void ValidHalfTurnQuaternionWithZeroWRotatesFormation()
    {
        var blob = CreateFormation(new int2(0, 0), new int2(2, 0));
        try
        {
            ref var grid = ref blob.Value.formations[0].gridPositions;
            float2 center = FormationPositionCalculator.CalculateFormationCenter(ref grid);
            FormationPositionCalculator.CalculateDesiredPosition(Entity.Null, ref grid, 0, center,
                default, null, float3.zero, out _, out var offset, out _, false,
                new quaternion(0, 1, 0, 0));
            Assert.That(offset.x, Is.EqualTo(1f).Within(0.0001f));
        }
        finally { blob.Dispose(); }
    }

    [Test]
    public void SurvivorKeepsItsStableSlotAfterBufferCompaction()
    {
        using var world = new World("Stable formation slot");
        var em = world.EntityManager;
        em.CreateEntity(typeof(MatchStateComponent));
        var blob = CreateFormation(new int2(0, 0), new int2(1, 0), new int2(2, 0));
        try
        {
            var squad = em.CreateEntity(typeof(SquadDefinitionComponent), typeof(SquadStateComponent),
                typeof(FormationComponent), typeof(SquadFormationAnchorComponent));
            em.SetComponentData(squad, new SquadDefinitionComponent { formationLibrary = blob });
            var survivor = em.CreateEntity(typeof(UnitGridSlotComponent), typeof(UnitTargetPositionComponent));
            em.SetComponentData(survivor, new UnitGridSlotComponent { slotIndex = 2 });
            em.AddBuffer<SquadUnitElement>(squad).Add(new SquadUnitElement { Value = survivor });
            world.GetOrCreateSystemManaged<GridFormationUpdateSystem>().Update();
            Assert.That(em.GetComponentData<UnitTargetPositionComponent>(survivor).position.x,
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(em.GetComponentData<UnitGridSlotComponent>(survivor).slotIndex, Is.EqualTo(2));
        }
        finally { blob.Dispose(); }
    }

    [Test]
    public void MalformedFormationLibraryIsIgnoredSafely()
    {
        using var world = new World("Malformed formation library");
        var em = world.EntityManager;
        em.CreateEntity(typeof(MatchStateComponent));
        var squad = em.CreateEntity(typeof(SquadDefinitionComponent), typeof(SquadStateComponent),
            typeof(FormationComponent), typeof(SquadFormationAnchorComponent));
        var unit = em.CreateEntity(typeof(UnitGridSlotComponent), typeof(UnitTargetPositionComponent));
        em.AddBuffer<SquadUnitElement>(squad).Add(new SquadUnitElement { Value = unit });
        Assert.DoesNotThrow(() => world.GetOrCreateSystemManaged<GridFormationUpdateSystem>().Update());
    }

    [Test]
    public void OutlierDoesNotPreventAnotherUnitFromBecomingFormed()
    {
        using var world = new World("Per-unit formation state");
        var em = world.EntityManager;
        em.CreateEntity(typeof(MatchStateComponent));
        em.AddComponentData(em.CreateEntity(), new SquadSpawnConfigComponent
        { slotArrivalThreshold = 0.2f, holdReformThreshold = 1 });
        var squad = em.CreateEntity(typeof(SquadStateComponent), typeof(SquadFormationAnchorComponent));
        em.AddComponent<SquadAnchorMovingTag>(squad);
        em.SetComponentEnabled<SquadAnchorMovingTag>(squad, false);
        var near = CreateUnit(em, float3.zero, float3.zero);
        var far = CreateUnit(em, new float3(50, 0, 0), float3.zero);
        var units = em.AddBuffer<SquadUnitElement>(squad);
        units.Add(new SquadUnitElement { Value = near });
        units.Add(new SquadUnitElement { Value = far });
        world.GetOrCreateSystem<UnitFormationStateSystem>().Update(world.Unmanaged);
        Assert.That(em.GetComponentData<UnitFormationStateComponent>(near).State, Is.EqualTo(UnitFormationState.Formed));
        Assert.That(em.GetComponentData<UnitFormationStateComponent>(far).State, Is.EqualTo(UnitFormationState.Moving));
    }

    static Entity CreateUnit(EntityManager em, float3 position, float3 target)
    {
        var unit = em.CreateEntity(typeof(LocalTransform), typeof(UnitGridSlotComponent),
            typeof(UnitTargetPositionComponent), typeof(UnitFormationStateComponent));
        em.SetComponentData(unit, LocalTransform.FromPosition(position));
        em.SetComponentData(unit, new UnitTargetPositionComponent { position = target });
        em.SetComponentData(unit, new UnitFormationStateComponent { State = UnitFormationState.Moving });
        return unit;
    }

    [TestCase(30)] [TestCase(60)] [TestCase(144)]
    public void AnchorMotionClassificationUsesSpeedNotFrameDisplacement(int fps)
    {
        float dt = 1f / fps;
        Assert.That(SquadAnchorSystem.IsMovingAtSpeed(float3.zero, new float3(5 * dt, 0, 0), dt, 4), Is.True);
        Assert.That(SquadAnchorSystem.IsMovingAtSpeed(float3.zero, new float3(3 * dt, 0, 0), dt, 4), Is.False);
    }
}
