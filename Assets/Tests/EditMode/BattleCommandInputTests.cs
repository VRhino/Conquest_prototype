using NUnit.Framework;
using Unity.Entities;
using UnityEngine;

/// <summary>The battle UI asks for commands through <see cref="BattleCommandInput"/>; the input systems treat them as keys (H10).</summary>
public class BattleCommandInputTests
{
    const int Sectors = 7;
    const float FirstAngle = -360f / Sectors;
    const float DeadZone = 90f;
    const float Outer = 300f;
    const float Radius = 210f;

    static readonly UISkillSlotData ReadySkill = new() { skillId = 7 };
    static readonly UISkillSlotData CoolingSkill = new() { skillId = 7, cooldown = 0.5f, remaining = 10f };

    [SetUp, TearDown]
    public void ResetInput() => BattleCommandInput.Reset();

    [Test]
    public void EverySectorCentreSelectsItsOwnSector()
    {
        for (int i = 0; i < Sectors; i++)
        {
            var centre = RadialMenuLayout.SectorCenter(i, Sectors, FirstAngle, Radius);
            Assert.That(RadialMenuLayout.SectorAt(centre, Sectors, FirstAngle, DeadZone, Outer), Is.EqualTo(i));
        }
    }

    [Test]
    public void TheSecondSectorGoesStraightUp()
    {
        var up = RadialMenuLayout.SectorCenter(1, Sectors, FirstAngle, Radius);
        Assert.That(up.x, Is.EqualTo(0f).Within(1e-3f));
        Assert.That(up.y, Is.EqualTo(Radius).Within(1e-3f));
    }

    [Test]
    public void NoSectorInTheDeadZoneOrOutsideTheMenu()
    {
        Assert.That(RadialMenuLayout.SectorAt(Vector2.up * (DeadZone - 1f), Sectors, FirstAngle, DeadZone, Outer),
            Is.EqualTo(RadialMenuLayout.NoSector), "The centre cancels.");
        Assert.That(RadialMenuLayout.SectorAt(Vector2.down * (Outer + 1f), Sectors, FirstAngle, DeadZone, Outer),
            Is.EqualTo(RadialMenuLayout.NoSector), "The way to the skill bar selects nothing.");
    }

    [Test]
    public void TheUiAsksForNothingWithoutPointerMode()
    {
        Assert.That(BattleCommandInput.TryRequestSquadSkill(SkillSlot.Squad1, ReadySkill), Is.False);
        Assert.That(BattleCommandInput.TryRequestFormation(0), Is.False);
        Assert.That(BattleCommandInput.ConsumeSquadSkill(), Is.Null);
        Assert.That(BattleCommandInput.ConsumeFormation(), Is.LessThan(0));
    }

    [Test]
    public void ACoolingEmptyOrHeroSlotAsksForNothing()
    {
        BattleCommandInput.SetPointerMode(true);
        Assert.That(BattleCommandInput.TryRequestSquadSkill(SkillSlot.Squad1, CoolingSkill), Is.False, "Cooling down.");
        Assert.That(BattleCommandInput.TryRequestSquadSkill(SkillSlot.Squad1, default), Is.False, "Not unlocked.");
        Assert.That(BattleCommandInput.TryRequestSquadSkill(SkillSlot.ActivePerk1, ReadySkill), Is.False, "Perks stay on their keys.");
        Assert.That(BattleCommandInput.TryRequestSquadSkill(SkillSlot.Q, ReadySkill), Is.False, "Class skills stay on their keys.");
        Assert.That(BattleCommandInput.ConsumeSquadSkill(), Is.Null);
    }

    [Test]
    public void ARequestIsTakenOnce()
    {
        BattleCommandInput.SetPointerMode(true);
        Assert.That(BattleCommandInput.TryRequestFormation(2), Is.True);
        Assert.That(BattleCommandInput.ConsumeFormation(), Is.EqualTo(2));
        Assert.That(BattleCommandInput.ConsumeFormation(), Is.LessThan(0));
    }

    [Test]
    public void ARequestMadeOnTheFrameAltIsReleasedSurvivesTheEndOfPointerMode()
    {
        BattleCommandInput.SetPointerMode(true);
        BattleCommandInput.TryRequestSquadSkill(SkillSlot.Squad2, ReadySkill);
        BattleCommandInput.SetPointerMode(false);
        Assert.That(BattleCommandInput.ConsumeSquadSkill(), Is.EqualTo(SkillSlot.Squad2));
    }

    [Test]
    public void AClickActsLikeOneFrameOfTheKeyAndReachesTheSquadRequest()
    {
        using var world = new World("Battle command input");
        var em = world.EntityManager;
        var squad = em.CreateEntity(typeof(SkillRequestComponent));
        em.SetComponentEnabled<SkillRequestComponent>(squad, false);
        var hero = em.CreateEntity(typeof(IsLocalPlayer), typeof(HeroInputComponent), typeof(SkillRequestComponent));
        em.SetComponentEnabled<SkillRequestComponent>(hero, false);
        em.AddComponentData(hero, new HeroSquadReference { squad = squad });
        var input = world.GetOrCreateSystemManaged<HeroInputSystem>();
        var request = world.GetOrCreateSystemManaged<HeroSkillRequestSystem>();

        BattleCommandInput.SetPointerMode(true);
        BattleCommandInput.TryRequestSquadSkill(SkillSlot.Squad2, ReadySkill);
        input.Update();
        Assert.That(em.GetComponentData<HeroInputComponent>(hero).UseSquadAbility2, Is.True);
        request.Update();
        Assert.That(em.IsComponentEnabled<SkillRequestComponent>(squad), Is.True);
        Assert.That(em.GetComponentData<SkillRequestComponent>(squad).slot, Is.EqualTo(SkillSlot.Squad2));

        input.Update();
        Assert.That(em.GetComponentData<HeroInputComponent>(hero).UseSquadAbility2, Is.False, "One frame, not a held key.");
    }

    [Test]
    public void BattleDataCarriesLifeAndCurrentFormation()
    {
        using var world = new World("Radial data");
        var em = world.EntityManager;
        em.CreateEntity(typeof(MatchStateComponent));
        var hero = em.CreateEntity(typeof(IsLocalPlayer));
        em.AddComponentData(hero, new HeroLifeComponent { isAlive = true });
        var squad = em.CreateEntity(typeof(IsLocalSquadActive));
        em.AddComponentData(squad, new FormationComponent { currentFormation = FormationType.Wedge });

        world.GetOrCreateSystemManaged<UIBattleDataSystem>().Update();

        using var query = em.CreateEntityQuery(typeof(UIHeroBattleDataComponent));
        var data = query.GetSingleton<UIHeroBattleDataComponent>();
        Assert.That(data.heroAlive, Is.True);
        Assert.That(data.currentFormation, Is.EqualTo(FormationType.Wedge));
    }
}
