using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

/// <summary>
/// F1: the templates per soldiers alive of Spearmen and Squires. Every count of soldiers finds a template that fits,
/// shapes survive their empty slots, roles stay and soldiers stay close to a slot between templates.
/// </summary>
public class FormationTemplateCoherenceTests
{
    /// <summary>Furthest a slot of a smaller template may sit from the nearest slot of the next larger one, in cells.</summary>
    const float MaxSlotShiftCells = 1.5f;

    const string SpearmenLine = "Assets/Resources/Squads/Spearmen/Formations/SpearmenLineFormation.asset";
    const string SpearmenWedge = "Assets/Resources/Squads/Spearmen/Formations/SpearmenWedgeFormation.asset";
    const string SquiresLine = "Assets/ScriptableObjects/Formations/baseLineFormation.asset";
    const string SquiresTestudo = "Assets/ScriptableObjects/Formations/baseTestudoFormation.asset";

    static FormationDefinition Load(string path) => AssetDatabase.LoadAssetAtPath<FormationDefinition>(path);

    [TestCase(SpearmenLine, 20, 15, 10, 5)]
    [TestCase(SpearmenWedge, 20, 15, 10, 5)]
    [TestCase(SquiresLine, 12, 10, 5)]
    [TestCase(SquiresTestudo, 12, 10, 5)]
    public void TemplateSizesAreTheThresholds(string path, params int[] sizes)
    {
        var formation = Load(path);
        Assert.That(formation.Validate(), Is.Null);
        Assert.That(formation.templates.Select(t => t.slots.Length), Is.EqualTo(sizes));
    }

    [TestCase(SpearmenLine)]
    [TestCase(SpearmenWedge)]
    [TestCase(SquiresLine)]
    [TestCase(SquiresTestudo)]
    public void EverySoldierCountUsesTheSmallestTemplateThatFits(string path)
    {
        var formation = Load(path);
        BlobAssetReference<FormationLibraryBlob> library = TestFormationLibrary.Build(formation.formationType, formation.layout,
            formation.spacing, formation.noiseRadius, formation.noiseAngleDegrees, formation.templates, formation.postures);
        try
        {
            for (int alive = 1; alive <= formation.Capacity; alive++)
            {
                int smallest = formation.templates.Where(t => t.slots.Length >= alive).Min(t => t.slots.Length);
                int chosen = FormationLayout.SelectTemplate(ref library.Value.formations[0], alive);
                Assert.That(formation.templates[chosen].slots.Length, Is.EqualTo(smallest), $"{alive} soldiers alive");
            }
        }
        finally { library.Dispose(); }
    }

    [TestCase(SpearmenLine)]
    [TestCase(SpearmenWedge)]
    [TestCase(SquiresLine)]
    [TestCase(SquiresTestudo)]
    public void SlotsFillFromTheFrontAndTheCentre(string path)
    {
        foreach (var template in Load(path).templates)
            for (int i = 1; i < template.slots.Length; i++)
            {
                var previous = template.slots[i - 1];
                var slot = template.slots[i];
                Assert.That(slot.row, Is.GreaterThanOrEqualTo(previous.row), $"{template.slots.Length} slots, slot {i}");
                if (slot.row == previous.row)
                    Assert.That(Mathf.Abs(slot.position.x), Is.GreaterThanOrEqualTo(Mathf.Abs(previous.position.x)),
                        $"{template.slots.Length} slots, slot {i}");
            }
    }

    [TestCase(SpearmenLine)]
    [TestCase(SpearmenWedge)]
    [TestCase(SquiresLine)]
    [TestCase(SquiresTestudo)]
    public void SmallerTemplatesStayNearTheLargerOne(string path)
    {
        var templates = Load(path).templates;
        for (int t = 1; t < templates.Length; t++)
            foreach (var slot in templates[t].slots)
            {
                float nearest = templates[t - 1].slots.Min(s => Vector2.Distance(s.position, slot.position));
                Assert.That(nearest, Is.LessThanOrEqualTo(MaxSlotShiftCells), $"{templates[t].slots.Length} slots, {slot.position}");
            }
    }

    [Test]
    public void UncrossPathsSwapsCrossingPaths()
    {
        var units = new NativeArray<float2>(new[] { new float2(-1, 0), new float2(1, 0) }, Allocator.Temp);
        var slots = new NativeArray<float2>(new[] { new float2(1, 2), new float2(-1, 2) }, Allocator.Temp);
        var slotOf = new NativeArray<int>(new[] { 0, 1 }, Allocator.Temp);

        FormationLayout.UncrossPaths(units, slots, slotOf);

        Assert.That(slotOf.ToArray(), Is.EqualTo(new[] { 1, 0 }));
    }

    [Test]
    public void UncrossPathsLeavesUnitsWithoutSlotAlone()
    {
        var units = new NativeArray<float2>(new[] { new float2(-1, 0), new float2(1, 0), new float2(0, 9) }, Allocator.Temp);
        var slots = new NativeArray<float2>(new[] { new float2(1, 2), new float2(-1, 2), new float2(0, 0) }, Allocator.Temp);
        var slotOf = new NativeArray<int>(new[] { 0, 1, -1 }, Allocator.Temp);

        FormationLayout.UncrossPaths(units, slots, slotOf);

        Assert.That(slotOf.ToArray(), Is.EqualTo(new[] { 1, 0, -1 }));
    }

    [Test]
    public void LinesKeepTheirTwoRanks()
    {
        foreach (string path in new[] { SpearmenLine, SquiresLine })
            foreach (var template in Load(path).templates)
                Assert.That(template.slots.Select(s => s.row).Distinct().OrderBy(r => r), Is.EqualTo(new[] { 0, 1 }));
    }

    [Test]
    public void TestudoKeepsEveryRoleAtEverySize()
    {
        foreach (var template in Load(SquiresTestudo).templates)
        {
            var roles = template.slots.Select(s => s.role).Distinct().ToArray();
            Assert.That(roles, Is.EquivalentTo(new[]
                { FormationSlotRole.Front, FormationSlotRole.Flank, FormationSlotRole.Interior, FormationSlotRole.Rear }));
        }
    }

    [Test]
    public void WedgeKeepsItsTipWidestAtTheBack()
    {
        foreach (var template in Load(SpearmenWedge).templates)
        {
            int[] widths = template.slots.GroupBy(s => s.row).OrderBy(g => g.Key).Select(g => g.Count()).ToArray();
            Assert.That(widths[0], Is.LessThan(widths.Max()), $"{template.slots.Length} slots");
            Assert.That(template.slots.Where(s => s.row == 0).All(s => s.role == FormationSlotRole.Front), Is.True);
        }
    }
}
