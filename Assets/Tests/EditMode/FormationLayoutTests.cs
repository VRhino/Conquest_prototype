using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>Data-driven formation layout: templates by soldiers alive, spacing, noise, Scatter, roles and rows.</summary>
public class FormationLayoutTests
{
    static FormationTemplate Template(int minUnits, int slots)
    {
        var positions = new Vector2[slots];
        for (int i = 0; i < slots; i++) positions[i] = new Vector2(i - (slots - 1) * 0.5f, 0f);
        return new FormationTemplate { minUnits = minUnits, slots = FormationSlotRoles.Assign(positions, false) };
    }

    [TestCase(10, 0)]
    [TestCase(12, 0)] // No template fits every soldier: the largest one.
    [TestCase(5, 1)]
    [TestCase(7, 0)]  // Template "≥ 5" has only 5 slots.
    [TestCase(2, 2)]
    public void TemplateIsTheFirstWhoseMinimumAndSlotsFit(int alive, int expected)
    {
        var blob = TestFormationLibrary.Build(FormationType.Line, FormationLayoutKind.Template, 1f, 0f, 0f,
            new[] { Template(10, 10), Template(5, 5), Template(0, 3) });
        try { Assert.That(FormationLayout.SelectTemplate(ref blob.Value.formations[0], alive), Is.EqualTo(expected)); }
        finally { blob.Dispose(); }
    }

    [Test]
    public void SlotOffsetIsItsPositionTimesTheSpacing()
    {
        var blob = TestFormationLibrary.FromCells(FormationType.Line, 1.1f, new int2(0, 0), new int2(3, 1));
        try
        {
            var slot = TestFormationLibrary.Slot(blob, 1, 1.1f);
            Assert.That(slot.localPosition, Is.EqualTo(new float2(1.5f, 0.5f)));
            Assert.That(slot.localOffset.x, Is.EqualTo(1.65f).Within(1e-5f));
            Assert.That(slot.localOffset.z, Is.EqualTo(0.55f).Within(1e-5f));
        }
        finally { blob.Dispose(); }
    }

    [Test]
    public void NoiseIsDeterministicAndBounded()
    {
        var blob = TestFormationLibrary.Build(FormationType.Line, FormationLayoutKind.Template, 1f, 0.08f, 3f,
            new[] { Template(0, 20) });
        try
        {
            ref var formation = ref blob.Value.formations[0];
            for (int i = 0; i < 20; i++)
            {
                FormationPositionCalculator.TryBuildSlot(ref formation, 0, i, 1f, 42u, out var a);
                FormationPositionCalculator.TryBuildSlot(ref formation, 0, i, 1f, 42u, out var b);
                Assert.That(a.localOffset, Is.EqualTo(b.localOffset));
                float2 clean = a.localPosition;
                Assert.That(math.distance(a.localOffset.xz, clean), Is.LessThanOrEqualTo(0.08f + 1e-5f));
                Assert.That(math.abs(a.headingLocal), Is.LessThanOrEqualTo(math.radians(3f) + 1e-5f));
            }
        }
        finally { blob.Dispose(); }
    }

    [Test]
    public void ScatterCellsAreDistinctAndLeaveTheAnchorFree()
    {
        int side = FormationLayout.ScatterSide(20);
        Assert.That(side % 2, Is.EqualTo(1));
        var seen = new System.Collections.Generic.HashSet<int2>();
        for (int k = 0; k < 20; k++)
        {
            int2 cell = FormationLayout.ScatterCell(side, k, 7u);
            Assert.That(cell, Is.Not.EqualTo(int2.zero));
            Assert.That(math.all(math.abs(cell) <= side / 2), Is.True);
            Assert.That(seen.Add(cell), Is.True, $"cell {cell} repeated");
        }
    }

    [Test]
    public void SpacingNeverFallsBelowTheFloor()
    {
        Assert.That(FormationLayout.EffectiveSpacing(1.1f, 1f), Is.EqualTo(1.1f).Within(1e-6f));
        Assert.That(FormationLayout.EffectiveSpacing(1f, 0.1f), Is.EqualTo(FormationDefinition.MinSpacing));
        Assert.That(FormationLayout.EffectiveSpacing(float.NaN, 1f), Is.EqualTo(FormationDefinition.MinSpacing));
    }

    [Test]
    public void MigrationGivesRowsRolesAndOutwardFlanks()
    {
        // 3 × 3 block: front row y = 2.
        var cells = new Vector2Int[9];
        for (int i = 0; i < 9; i++) cells[i] = new Vector2Int(i % 3, i / 3);
        var slots = FormationSlotRoles.FromCells(cells, true);
        Assert.That(slots[7].row, Is.EqualTo(0));
        Assert.That(slots[7].role, Is.EqualTo(FormationSlotRole.Front));
        Assert.That(slots[3].role, Is.EqualTo(FormationSlotRole.Flank));
        Assert.That(slots[3].headingDegrees, Is.EqualTo(-90f));
        Assert.That(slots[5].headingDegrees, Is.EqualTo(90f));
        Assert.That(slots[4].role, Is.EqualTo(FormationSlotRole.Interior));
        Assert.That(slots[1].role, Is.EqualTo(FormationSlotRole.Rear));
        Assert.That(slots[1].headingDegrees, Is.EqualTo(180f));
        Assert.That(slots[1].row, Is.EqualTo(2));
    }

    [Test]
    public void DefinitionValidationCatchesBadTemplates()
    {
        var definition = ScriptableObject.CreateInstance<FormationDefinition>();
        try
        {
            definition.templates = new[] { Template(0, 3), Template(5, 5) };
            Assert.That(definition.Validate(), Does.Contain("sorted"));
            definition.templates = new[] { new FormationTemplate { slots = new[] { new FormationSlot(), new FormationSlot() } } };
            Assert.That(definition.Validate(), Does.Contain("repeats"));
            definition.templates = new[] { Template(5, 5), Template(0, 3) };
            Assert.That(definition.Validate(), Is.Null);
            Assert.That(definition.Capacity, Is.EqualTo(5));
        }
        finally { Object.DestroyImmediate(definition); }
    }

    [Test]
    public void CasualtiesSwitchToTheSmallerTemplateWithoutReorderingUnits()
    {
        using var world = new World("Template by soldiers alive");
        var em = world.EntityManager;
        em.CreateEntity(typeof(MatchStateComponent));
        var blob = TestFormationLibrary.Build(FormationType.Line, FormationLayoutKind.Template, 1f, 0f, 0f,
            new[] { Template(3, 3), Template(0, 2) });
        try
        {
            var squad = em.CreateEntity(typeof(SquadResolvedOrderComponent), typeof(SquadStateComponent),
                typeof(FormationComponent), typeof(SquadDefinitionComponent));
            em.AddComponentData(squad, new SquadFormationAnchorComponent { rotation = quaternion.identity });
            em.SetComponentData(squad, new SquadDefinitionComponent { formationLibrary = blob });
            em.SetComponentData(squad, new FormationComponent
            { currentFormation = FormationType.Line, templateIndex = 0, layoutUnitCount = 3, spacing = 1f, scale = 1f });
            var units = new Entity[3];
            for (int i = 0; i < 3; i++)
            {
                units[i] = em.CreateEntity(typeof(UnitTargetPositionComponent));
                em.AddComponentData(units[i], TestFormationLibrary.Slot(blob, i));
            }
            var buffer = em.AddBuffer<SquadUnitElement>(squad);
            buffer.Add(new SquadUnitElement { Value = units[1] });
            buffer.Add(new SquadUnitElement { Value = units[2] }); // units[0] died.

            world.GetOrCreateSystemManaged<FormationSystem>().Update();

            var formation = em.GetComponentData<FormationComponent>(squad);
            Assert.That(formation.templateIndex, Is.EqualTo(1));
            Assert.That(formation.layoutUnitCount, Is.EqualTo(2));
            Assert.That(em.GetComponentData<UnitGridSlotComponent>(units[1]).slotIndex, Is.EqualTo(0));
            Assert.That(em.GetComponentData<UnitGridSlotComponent>(units[2]).slotIndex, Is.EqualTo(1));
            Assert.That(em.GetComponentData<UnitGridSlotComponent>(units[1]).localPosition.x, Is.EqualTo(-0.5f));
        }
        finally { blob.Dispose(); }
    }

    [Test]
    public void SpacingRequestScalesTheLayoutAndIsConsumed()
    {
        using var world = new World("Spacing request");
        var em = world.EntityManager;
        em.CreateEntity(typeof(MatchStateComponent));
        var blob = TestFormationLibrary.FromCells(FormationType.Line, 1f, new int2(0, 0), new int2(2, 0));
        try
        {
            var squad = em.CreateEntity(typeof(SquadResolvedOrderComponent), typeof(SquadStateComponent),
                typeof(FormationComponent), typeof(SquadDefinitionComponent));
            em.AddComponentData(squad, new SquadFormationAnchorComponent { rotation = quaternion.identity });
            em.SetComponentData(squad, new SquadDefinitionComponent { formationLibrary = blob });
            em.SetComponentData(squad, new FormationComponent
            { currentFormation = FormationType.Line, templateIndex = 0, layoutUnitCount = 2, spacing = 1f, scale = 1f });
            var unit = em.CreateEntity(typeof(UnitTargetPositionComponent));
            em.AddComponentData(unit, TestFormationLibrary.Slot(blob, 1));
            var other = em.CreateEntity(typeof(UnitTargetPositionComponent));
            em.AddComponentData(other, TestFormationLibrary.Slot(blob, 0));
            var buffer = em.AddBuffer<SquadUnitElement>(squad);
            buffer.Add(new SquadUnitElement { Value = other });
            buffer.Add(new SquadUnitElement { Value = unit });
            em.AddComponentData(squad, new SquadFormationSpacingRequest { scale = 1.5f });

            world.GetOrCreateSystemManaged<FormationSystem>().Update();

            Assert.That(em.HasComponent<SquadFormationSpacingRequest>(squad), Is.False);
            Assert.That(em.GetComponentData<FormationComponent>(squad).spacing, Is.EqualTo(1.5f).Within(1e-5f));
            Assert.That(em.GetComponentData<UnitGridSlotComponent>(unit).localOffset.x, Is.EqualTo(1.5f).Within(1e-5f));
        }
        finally { blob.Dispose(); }
    }
}
