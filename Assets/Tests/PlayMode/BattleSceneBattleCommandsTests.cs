#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

/// <summary>ALT frees the cursor; the skill bar and the radial menu launch squad abilities and change formation (H10).</summary>
public partial class BattleSceneIntegrationTests
{
    [UnityTest, Category("BattleCommands")]
    public IEnumerator BarClickLaunchesTheSquadAbilityWithoutAttacking()
    {
        yield return UnlockFirstSquadAbilityInBattle();
        var skill = em.GetBuffer<HeroSkillElement>(squad)[0];
        var slot = (RectTransform)Object.FindFirstObjectByType<SkillBarController>().transform.Find("Bar/Slot_Squad1");

        yield return Click(slot, reachesHero: true);
        Assert.That(UnitsWithModifier(skill.skillId), Is.EqualTo(0), "With a locked cursor the bar takes no clicks.");
        yield return Until(() => !SkillInstanceActive(), 5f, "The attack of the click outside pointer mode did not end.");

        yield return HoldAlt();
        Assert.That(BattleCommandInput.PointerMode, Is.True);
        Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None), "ALT frees the cursor.");
        yield return Click(slot);
        yield return Until(() => UnitsWithModifier(skill.skillId) > 0, 5f, "The click did not launch the squad's ability.");
        Assert.That(SkillInstanceActive(), Is.False, "The click must not attack.");

        double cooling = CooldownEnd(skill);
        Assert.That(cooling, Is.GreaterThan(0d), "The squad cools down.");
        yield return Click(slot);
        Assert.That(CooldownEnd(skill), Is.EqualTo(cooling), "A cooling slot launches nothing.");

        yield return ReleaseAlt();
        Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked), "Releasing ALT locks the cursor again.");
    }

    [UnityTest, Category("BattleCommands")]
    public IEnumerator HoldingTheButtonOutOfPointerModeDoesNotAttackUntilItIsReleased()
    {
        yield return HoldAlt();
        InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
        yield return Frames(3);
        Assert.That(em.GetComponentData<HeroInputComponent>(hero).IsAttackPressed, Is.False);
        yield return ReleaseAlt();
        Assert.That(em.GetComponentData<HeroInputComponent>(hero).IsAttackPressed, Is.False, "The button still belongs to the UI.");
        InputSystem.QueueStateEvent(mouse, new MouseState());
        yield return Frames(3);
        InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
        yield return Frames(3);
        Assert.That(em.GetComponentData<HeroInputComponent>(hero).IsAttackPressed, Is.True, "A new click attacks.");
        InputSystem.QueueStateEvent(mouse, new MouseState());
        yield return Frames(3);
    }

    [UnityTest, Category("BattleCommands")]
    public IEnumerator RadialMenuLaunchesOnReleaseChangesFormationOnClickAndCancelsInTheCentre()
    {
        yield return UnlockFirstSquadAbilityInBattle();
        var skill = em.GetBuffer<HeroSkillElement>(squad)[0];
        var menu = Object.FindFirstObjectByType<BattleRadialMenu>();
        Assert.That(menu, Is.Not.Null, "The HUD did not create the radial menu.");
        var panel = (RectTransform)menu.transform.Find("Panel");
        Assert.That(panel.gameObject.activeSelf, Is.False);

        yield return HoldAlt();
        Assert.That(panel.gameObject.activeSelf, Is.True, "ALT shows the menu.");
        var centre = panel;
        var ability = (RectTransform)panel.Find("Skill1");
        var formation = (RectTransform)panel.Find("Formation2");
        Assert.That(formation.gameObject.activeSelf, Is.True, "The squad's formations are in the menu.");

        PointAt(centre, false);
        yield return Frames(3);
        yield return ReleaseAlt();
        Assert.That(CooldownEnd(skill), Is.EqualTo(0d), "Releasing in the centre cancels.");

        yield return HoldAlt();
        var start = em.GetComponentData<FormationComponent>(squad).currentFormation;
        var library = em.GetComponentData<SquadDefinitionComponent>(squad).formationLibrary;
        var target = library.Value.formations[1].formationType;
        Assert.That(target, Is.Not.EqualTo(start), "The test needs the second formation to differ.");
        yield return Click(formation);
        yield return Until(() => em.GetComponentData<FormationComponent>(squad).currentFormation == target, 10f,
            "A click on the sector did not change the formation.");

        PointAt(ability, false);
        yield return Frames(3);
        yield return ReleaseAlt();
        yield return Until(() => UnitsWithModifier(skill.skillId) > 0, 5f, "Releasing ALT over the sector did not launch the ability.");
        Assert.That(SkillInstanceActive(), Is.False, "The menu must not attack.");
        Assert.That(panel.gameObject.activeSelf, Is.False, "Releasing ALT closes the menu.");
    }

    [UnityTest, Category("BattleCommands")]
    public IEnumerator SkillBarShowsTheComboAndDimsTheUltimateWhileItIsShort()
    {
        var ultimate = Object.FindFirstObjectByType<SkillBarController>().transform.Find("Bar/Slot_Ultimate");
        var counter = ultimate.Find("ComboBadge/Combo").GetComponent<TMPro.TMP_Text>();
        var dimmed = ultimate.Find("CooldownFill").GetComponent<UnityEngine.UI.Image>();
        double now = World.DefaultGameObjectInjectionWorld.Time.ElapsedTime;
        em.SetComponentData(hero, new HeroResourcesComponent { combo = 0f, lastHitTime = now });
        yield return Until(() => counter.isActiveAndEnabled && counter.text == "0/20", 5f, "The ultimate does not show the combo it lacks.");
        Assert.That(dimmed.enabled, Is.True, "Without combo the ultimate is dimmed.");

        now = World.DefaultGameObjectInjectionWorld.Time.ElapsedTime;
        em.SetComponentData(hero, new HeroResourcesComponent { combo = 25f, lastHitTime = now });
        yield return Until(() => counter.text == "25/20" && !dimmed.enabled, 5f, "With enough combo the ultimate lights up.");
    }

    IEnumerator UnlockFirstSquadAbilityInBattle()
    {
        var progress = em.GetComponentData<SquadProgressComponent>(squad);
        progress.level = SquadProgressionCurves.UnlockLevel(em.GetComponentData<SquadDataComponent>(squad).curves, 0);
        em.SetComponentData(squad, progress);
        em.SetComponentData(em.CreateEntity(typeof(SquadLevelUpEvent)), new SquadLevelUpEvent { squad = squad });
        yield return Until(() => em.GetBuffer<HeroSkillElement>(squad).Length == 1, 5f, "The first ability did not unlock at its level.");
        yield return Until(() => em.GetComponentData<MatchStateComponent>(GetMatchEntity()).currentState == MatchState.InBattle,
            60f, "The battle did not start.");
    }

    double CooldownEnd(HeroSkillElement skill)
    {
        foreach (var group in em.GetBuffer<CooldownGroupElement>(squad))
            if (group.groupId == skill.skill.Value.cooldownGroup) return group.endTime;
        return 0d;
    }

    bool SkillInstanceActive() => em.GetComponentData<SkillInstanceComponent>(hero).active;

    IEnumerator HoldAlt()
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftAlt));
        yield return Frames(3);
    }

    IEnumerator ReleaseAlt()
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return Frames(3);
    }

    void PointAt(RectTransform target, bool pressed)
    {
        var state = new MouseState { position = RectTransformUtility.WorldToScreenPoint(null, target.TransformPoint(target.rect.center)) };
        InputSystem.QueueStateEvent(mouse, pressed ? state.WithButton(MouseButton.Left) : state);
    }

    IEnumerator Click(RectTransform target, bool reachesHero = false)
    {
        PointAt(target, false);
        yield return Frames(3);
        PointAt(target, true);
        yield return Frames(3);
        Assert.That(em.GetComponentData<HeroInputComponent>(hero).IsAttackPressed, Is.EqualTo(reachesHero), "Only a click outside pointer mode reaches the hero.");
        PointAt(target, false);
        yield return Frames(3);
    }

    static IEnumerator Frames(int count)
    {
        for (int i = 0; i < count; i++) yield return null;
    }
}
#endif
