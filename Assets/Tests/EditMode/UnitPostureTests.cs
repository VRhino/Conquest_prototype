using ConquestTactics.Animation;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEditor;

/// <summary>Posture per slot role (paso 6): the formation's table, chosen by UnitPostureSystem.</summary>
public class UnitPostureTests
{
    [Test]
    public void SpearmenLineSlotsFeedBothRanksThenClearWhenMovingOrChangingFormation()
    {
        var line = AssetDatabase.LoadAssetAtPath<FormationDefinition>(
            "Assets/Resources/Squads/Spearmen/Formations/SpearmenLineFormation.asset");
        var wedge = AssetDatabase.LoadAssetAtPath<FormationDefinition>(
            "Assets/Resources/Squads/Spearmen/Formations/SpearmenWedgeFormation.asset");
        Assert.That(line, Is.Not.Null);
        Assert.That(wedge, Is.Not.Null);
        using var world = new World("Spearmen Line posture integration");
        var em = world.EntityManager;
        em.CreateEntity(typeof(MatchStateComponent));
        var lineBlob = TestFormationLibrary.Build(line.formationType, line.layout, line.spacing,
            line.noiseRadius, line.noiseAngleDegrees, line.templates, line.postures);
        var wedgeBlob = TestFormationLibrary.Build(wedge.formationType, wedge.layout, wedge.spacing,
            wedge.noiseRadius, wedge.noiseAngleDegrees, wedge.templates, wedge.postures);
        try
        {
            var squad = em.CreateEntity(typeof(FormationComponent), typeof(SquadDefinitionComponent));
            em.SetComponentData(squad, new FormationComponent { currentFormation = FormationType.Line });
            em.SetComponentData(squad, new SquadDefinitionComponent { formationLibrary = lineBlob });
            var units = new System.Collections.Generic.List<Entity>();
            foreach (var slot in line.templates[0].slots)
            {
                var unit = em.CreateEntity(typeof(UnitFormationStanceComponent), typeof(UnitAnimationMovementComponent));
                em.AddComponentData(unit, new UnitGridSlotComponent { role = slot.role, row = slot.row });
                em.AddComponentData(unit, new UnitFormationStateComponent { State = UnitFormationState.Formed });
                units.Add(unit);
            }
            var buffer = em.AddBuffer<SquadUnitElement>(squad);
            foreach (var unit in units) buffer.Add(new SquadUnitElement { Value = unit });
            var system = world.GetOrCreateSystem<UnitPostureSystem>();
            system.Update(world.Unmanaged);
            foreach (var unit in units)
            {
                var row = em.GetComponentData<UnitGridSlotComponent>(unit).row;
                var expected = row == 0 ? UnitStance.SpearLineFront : UnitStance.SpearLineRear;
                Assert.That(em.GetComponentData<UnitAnimationMovementComponent>(unit).CurrentStance, Is.EqualTo(expected));
                em.SetComponentData(unit, new UnitFormationStateComponent { State = UnitFormationState.Moving });
            }
            system.Update(world.Unmanaged);
            foreach (var unit in units)
            {
                Assert.That(em.GetComponentData<UnitAnimationMovementComponent>(unit).CurrentStance, Is.EqualTo(UnitStance.Normal));
                em.SetComponentData(unit, new UnitFormationStateComponent { State = UnitFormationState.Formed });
            }
            system.Update(world.Unmanaged);
            em.SetComponentData(squad, new FormationComponent { currentFormation = FormationType.Wedge });
            em.SetComponentData(squad, new SquadDefinitionComponent { formationLibrary = wedgeBlob });
            system.Update(world.Unmanaged);
            foreach (var unit in units)
                Assert.That(em.GetComponentData<UnitAnimationMovementComponent>(unit).CurrentStance, Is.EqualTo(UnitStance.Normal));
        }
        finally { lineBlob.Dispose(); wedgeBlob.Dispose(); }
    }

    static readonly FormationRolePosture[] Testudo =
    {
        new() { role = FormationSlotRole.Front, formed = UnitStance.ShieldFront, moving = UnitStance.ShieldFront },
        new() { role = FormationSlotRole.Flank, formed = UnitStance.ShieldSide, moving = UnitStance.ShieldSide },
        new() { role = FormationSlotRole.Interior, formed = UnitStance.ShieldUp, moving = UnitStance.Normal },
    };

    static BlobAssetReference<FormationLibraryBlob> TestudoLibrary() =>
        TestFormationLibrary.Build(FormationType.Testudo, FormationLayoutKind.Template, 0.8f, 0f, 0f,
            new[] { new FormationTemplate { slots = new[] { new FormationSlot() } } }, Testudo);

    [Test]
    public void TableGivesEachRoleItsPostureAndNormalOtherwise()
    {
        var blob = TestudoLibrary();
        try
        {
            ref var formation = ref blob.Value.formations[0];
            Assert.That(FormationLayout.Posture(ref formation, FormationSlotRole.Interior, false), Is.EqualTo(UnitStance.ShieldUp));
            Assert.That(FormationLayout.Posture(ref formation, FormationSlotRole.Interior, true), Is.EqualTo(UnitStance.Normal));
            Assert.That(FormationLayout.Posture(ref formation, FormationSlotRole.Flank, true), Is.EqualTo(UnitStance.ShieldSide));
            Assert.That(FormationLayout.Posture(ref formation, FormationSlotRole.Rear, false), Is.EqualTo(UnitStance.Normal));
        }
        finally { blob.Dispose(); }
    }

    [Test]
    public void SystemSetsPostureByRoleAndStateAndFeedsTheAnimation()
    {
        using var world = new World("Posture by slot role");
        var em = world.EntityManager;
        em.CreateEntity(typeof(MatchStateComponent));
        var blob = TestudoLibrary();
        try
        {
            var squad = em.CreateEntity(typeof(FormationComponent), typeof(SquadDefinitionComponent));
            em.SetComponentData(squad, new FormationComponent { currentFormation = FormationType.Testudo });
            em.SetComponentData(squad, new SquadDefinitionComponent { formationLibrary = blob });
            Entity Unit(FormationSlotRole role, int row, UnitFormationState state)
            {
                var unit = em.CreateEntity(typeof(UnitFormationStanceComponent), typeof(UnitAnimationMovementComponent));
                em.AddComponentData(unit, new UnitGridSlotComponent { role = role, row = row });
                em.AddComponentData(unit, new UnitFormationStateComponent { State = state });
                return unit;
            }
            var interiorFormed = Unit(FormationSlotRole.Interior, 1, UnitFormationState.Formed);
            var interiorMoving = Unit(FormationSlotRole.Interior, 1, UnitFormationState.Moving);
            var flank = Unit(FormationSlotRole.Flank, 2, UnitFormationState.Formed);
            var buffer = em.AddBuffer<SquadUnitElement>(squad);
            foreach (var u in new[] { interiorFormed, interiorMoving, flank }) buffer.Add(new SquadUnitElement { Value = u });

            world.GetOrCreateSystem<UnitPostureSystem>().Update(world.Unmanaged);

            Assert.That(em.GetComponentData<UnitFormationStanceComponent>(interiorFormed).stance, Is.EqualTo(UnitStance.ShieldUp));
            Assert.That(em.GetComponentData<UnitFormationStanceComponent>(interiorMoving).stance, Is.EqualTo(UnitStance.Normal));
            Assert.That(em.GetComponentData<UnitFormationStanceComponent>(flank).stance, Is.EqualTo(UnitStance.ShieldSide));
            var anim = em.GetComponentData<UnitAnimationMovementComponent>(flank);
            Assert.That(anim.CurrentStance, Is.EqualTo(UnitStance.ShieldSide));
            Assert.That(anim.SlotRow, Is.EqualTo(2));
            Assert.That(anim.SlotRole, Is.EqualTo((int)FormationSlotRole.Flank));
        }
        finally { blob.Dispose(); }
    }
}
