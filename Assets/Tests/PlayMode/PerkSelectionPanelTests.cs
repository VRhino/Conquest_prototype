#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using Core.Persistence;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>The fief perk panel (H5): it lists the class perks and its buttons write and save the active loadout.</summary>
public class PerkSelectionPanelTests
{
    sealed class MemorySave : ISaveProvider
    {
        public int saves;
        public PlayerData Player;
        public void Save(PlayerData data) { Player = data; saves++; }
        public PlayerData Load() => Player;
    }

    ISaveProvider _previousSave;
    PlayerData _previousPlayer;
    HeroData _previousHero;
    GameObject _panel;

    static FieldInfo ProviderField => typeof(SaveSystem).GetField("_provider", BindingFlags.Static | BindingFlags.NonPublic);

    [SetUp]
    public void SetUp()
    {
        _previousSave = (ISaveProvider)ProviderField.GetValue(null);
        _previousPlayer = PlayerSessionService.CurrentPlayer;
        _previousHero = PlayerSessionService.SelectedHero;
    }

    [TearDown]
    public void TearDown()
    {
        if (_panel != null) Object.Destroy(_panel);
        SaveSystem.SetProvider(_previousSave);
        PlayerSessionService.Clear();
        if (_previousPlayer != null) PlayerSessionService.SetPlayer(_previousPlayer);
        if (_previousHero != null) PlayerSessionService.SetSelectedHero(_previousHero);
    }

    [UnityTest]
    public IEnumerator ThePanelListsTheClassPerksAndSavesTheChoiceInTheActiveLoadout()
    {
        var memory = new MemorySave();
        SaveSystem.SetProvider(memory);
        var hero = new HeroData { heroName = "PerkPanel", classId = "SwordAndShield" };
        var player = new PlayerData { playerName = "PerkPanelFixture" };
        player.heroes.Add(hero);
        PlayerSessionService.SetPlayer(player);
        PlayerSessionService.SetSelectedHero(hero);
        var classPerks = PerkSelection.Available(HeroClassManager.GetClassDefinition(hero.classId).validClassPerks, hero);
        Assume.That(classPerks.Count, Is.GreaterThan(0), "The class has perks to choose.");

        var prefab = Resources.Load<PerkSelectionPanelController>(PerkSelectionPanelController.ResourcePath);
        Assert.That(prefab, Is.Not.Null, "The panel loads from Resources, without a scene reference.");
        var panel = Object.Instantiate(prefab);
        _panel = panel.gameObject;
        Assert.That(panel.IsPanelOpen, Is.False, "Closed until asked.");
        panel.OpenPanel();
        yield return null;

        var rows = panel.GetComponentsInChildren<PerkSelectionRow>(false);
        Assert.That(rows.Length, Is.EqualTo(classPerks.Count), "One row per available perk, the template hidden.");
        var button = rows[0].GetComponentInChildren<UnityEngine.UI.Button>();
        button.onClick.Invoke();
        yield return null;

        Assert.That(PerkLoadout.ChosenIds(hero), Is.EqualTo(new[] { classPerks[0].perkID }));
        Assert.That(memory.saves, Is.EqualTo(1), "Saved at once.");

        panel.ClosePanel();
        panel.OpenPanel();
        yield return null;
        Assert.That(panel.GetComponentsInChildren<PerkSelectionRow>(false).Length, Is.EqualTo(classPerks.Count), "Reopening does not duplicate rows.");
        rows[0].GetComponentInChildren<UnityEngine.UI.Button>().onClick.Invoke();
        Assert.That(PerkLoadout.ChosenIds(hero), Is.Empty, "The same button removes it.");
    }
}
#endif
