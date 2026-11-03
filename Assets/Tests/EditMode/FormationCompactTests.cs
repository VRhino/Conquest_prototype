using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>Closing the gaps casualties leave inside one template (Aprendizajes_Referencia.md L3-02).</summary>
public class FormationCompactTests
{
    static int Compact(float2[] units, float2[] slots, int[] slotOf, out int[] result)
    {
        using var u = new NativeArray<float2>(units, Allocator.Temp);
        using var s = new NativeArray<float2>(slots, Allocator.Temp);
        using var o = new NativeArray<int>(slotOf, Allocator.Temp);
        int moved = FormationLayout.CompactSlots(u, s, o);
        result = o.ToArray();
        return moved;
    }

    static readonly float2[] Row = { new float2(0, 0), new float2(1, 0), new float2(2, 0), new float2(3, 0) };

    [Test]
    public void OnlyTheSoldiersPastTheAliveCountMove()
    {
        // Slot 1 lost its soldier; the one on slot 3 (past the 3 alive) takes it, the rest stay.
        int moved = Compact(new[] { Row[0], Row[2], new float2(3, 0) }, new[] { Row[0], Row[1], Row[2] },
            new[] { 0, 2, 3 }, out var slots);
        Assert.That(moved, Is.EqualTo(1));
        Assert.That(slots, Is.EqualTo(new[] { 0, 2, 1 }));
    }

    [Test]
    public void RepeatedOrMissingSlotsMoveAndTheMoversDoNotCross()
    {
        // Two soldiers past the count, each nearer the other's gap if matched badly: no crossing.
        var units = new[] { new float2(0, 0), new float2(3, 1), new float2(1, 1) };
        int moved = Compact(units, new[] { Row[0], Row[1], Row[2] }, new[] { 0, 5, 7 }, out var slots);
        Assert.That(moved, Is.EqualTo(2));
        Assert.That(slots, Is.EqualTo(new[] { 0, 2, 1 }));

        moved = Compact(new[] { Row[0], Row[1] }, new[] { Row[0], Row[1] }, new[] { 0, 0 }, out slots);
        Assert.That(moved, Is.EqualTo(1), "A repeated slot is a gap for the second one.");
        Assert.That(slots, Is.EqualTo(new[] { 0, 1 }));
    }

    [Test]
    public void FormationSystemWaitsTheDelayThenClosesTheGap()
    {
        using var world = new World("Formation compact");
        var em = world.EntityManager;
        em.CreateEntity(typeof(MatchStateComponent));
        var blob = TestFormationLibrary.FromCells(FormationType.Line, 1f,
            new int2(0, 0), new int2(1, 0), new int2(2, 0), new int2(3, 0));
        try
        {
            var squad = em.CreateEntity(typeof(SquadResolvedOrderComponent), typeof(SquadStateComponent),
                typeof(FormationComponent), typeof(SquadDefinitionComponent), typeof(FormationChangePending));
            em.SetComponentData(squad, new SquadDefinitionComponent { formationLibrary = blob });
            em.SetComponentData(squad, new FormationComponent
            {
                currentFormation = FormationType.Line, templateIndex = 0, layoutUnitCount = 4, spacing = 1f, scale = 1f,
            });
            em.AddComponentData(squad, new SquadFormationAnchorComponent { rotation = quaternion.identity });
            var units = em.AddBuffer<SquadUnitElement>(squad);
            var entities = new Entity[3];
            int[] held = { 0, 2, 3 }; // The soldier of slot 1 died.
            for (int i = 0; i < 3; i++)
            {
                entities[i] = em.CreateEntity(typeof(LocalTransform), typeof(UnitGridSlotComponent));
                em.SetComponentData(entities[i], new UnitGridSlotComponent { slotIndex = held[i] });
                em.GetBuffer<SquadUnitElement>(squad).Add(new SquadUnitElement { Value = entities[i] });
            }
            var system = world.GetOrCreateSystemManaged<FormationSystem>();

            world.SetTime(new TimeData(10.0, 0.02f));
            system.Update();
            Assert.That(em.GetComponentData<UnitGridSlotComponent>(entities[2]).slotIndex, Is.EqualTo(3), "Waiting.");
            Assert.That(em.IsComponentEnabled<FormationChangePending>(squad), Is.True, "Still pending while it waits.");
            double due = em.GetComponentData<FormationComponent>(squad).compactAt;
            Assert.That(due, Is.EqualTo(10.0 + MovementTuningComponent.Default.formationCompactDelay).Within(1e-6));

            world.SetTime(new TimeData(due, 0.02f));
            system.Update();
            Assert.That(em.GetComponentData<UnitGridSlotComponent>(entities[0]).slotIndex, Is.EqualTo(0));
            Assert.That(em.GetComponentData<UnitGridSlotComponent>(entities[1]).slotIndex, Is.EqualTo(2));
            Assert.That(em.GetComponentData<UnitGridSlotComponent>(entities[2]).slotIndex, Is.EqualTo(1), "Took the gap.");
            var formation = em.GetComponentData<FormationComponent>(squad);
            Assert.That(formation.layoutUnitCount, Is.EqualTo(3));
            Assert.That(formation.compactAt, Is.EqualTo(0.0));
            Assert.That(em.IsComponentEnabled<FormationChangePending>(squad), Is.False);
        }
        finally { blob.Dispose(); }
    }
}
