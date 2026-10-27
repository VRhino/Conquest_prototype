using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>Choosing perks in the fief (H5): what is available and the 5 + 2 limits of the active loadout.</summary>
public class PerkSelectionTests
{
    readonly List<HeroPerk> _created = new();

    HeroPerk Perk(int id, bool passive, bool unlocked = true)
    {
        var perk = ScriptableObject.CreateInstance<HeroPerk>();
        perk.perkID = id;
        perk.isPassive = passive;
        perk.isUnlockedByDefault = unlocked;
        _created.Add(perk);
        return perk;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var perk in _created) Object.DestroyImmediate(perk);
        _created.Clear();
    }

    [Test]
    public void OnlyUnlockedClassPerksAreAvailable()
    {
        var open = Perk(1, true);
        var locked = Perk(2, true, unlocked: false);
        var earned = Perk(3, false, unlocked: false);
        var hero = new HeroData { unlockedPerks = new List<int> { 3 } };
        Assert.That(PerkSelection.Available(new[] { open, locked, earned, null }, hero), Is.EqualTo(new[] { open, earned }));
    }

    [Test]
    public void ToggleWritesTheActiveLoadoutAndCreatesOneIfNone()
    {
        var perk = Perk(7, true);
        var available = new[] { perk };
        var hero = new HeroData();
        Assert.That(PerkSelection.Toggle(hero, available, perk), Is.True);
        Assert.That(hero.loadouts.Count, Is.EqualTo(1));
        Assert.That(hero.loadouts[0].isActive, Is.True);
        Assert.That(hero.loadouts[0].perkIDs, Is.EqualTo(new[] { "7" }));
        Assert.That(PerkLoadout.ChosenIds(hero), Is.EqualTo(new[] { 7 }), "The battle reads what the panel wrote.");

        Assert.That(PerkSelection.Toggle(hero, available, perk), Is.True);
        Assert.That(hero.loadouts[0].perkIDs, Is.Empty, "A second toggle removes it.");
    }

    [Test]
    public void TheActiveLoadoutIsTheOneTheBattleReads()
    {
        var perk = Perk(5, false);
        var hero = new HeroData();
        hero.loadouts.Add(new LoadoutSaveData { name = "A" });
        hero.loadouts.Add(new LoadoutSaveData { name = "B", isActive = true });
        PerkSelection.Toggle(hero, new[] { perk }, perk);
        Assert.That(hero.loadouts[1].perkIDs, Is.EqualTo(new[] { "5" }));
        Assert.That(hero.loadouts[0].perkIDs, Is.Empty);
    }

    [Test]
    public void FivePassivesAndTwoActivesAtMost()
    {
        var available = new List<HeroPerk>();
        for (int i = 0; i < 6; i++) available.Add(Perk(10 + i, true));
        for (int i = 0; i < 3; i++) available.Add(Perk(20 + i, false));
        var hero = new HeroData();
        foreach (var perk in available) PerkSelection.Toggle(hero, available, perk);

        PerkSelection.Count(hero, available, out int passives, out int actives);
        Assert.That(passives, Is.EqualTo(PerkLoadout.MaxPassive));
        Assert.That(actives, Is.EqualTo(PerkLoadout.MaxActive));
        Assert.That(PerkSelection.CanAdd(hero, available, available[5]), Is.False, "The sixth passive does not fit.");
        Assert.That(PerkSelection.Toggle(hero, available, available[8]), Is.False, "Nor the third active.");

        PerkSelection.Toggle(hero, available, available[0]);
        Assert.That(PerkSelection.CanAdd(hero, available, available[5]), Is.True, "Removing one frees its place.");
    }

    [Test]
    public void IdsTheClassNoLongerOffersAreDroppedWhenSaving()
    {
        var perk = Perk(1, true);
        var hero = new HeroData();
        hero.loadouts.Add(new LoadoutSaveData { isActive = true, perkIDs = new List<string> { "99", "1" } });
        Assert.That(PerkSelection.IsChosen(hero, perk), Is.True);
        PerkSelection.Toggle(hero, new[] { perk }, perk);
        Assert.That(hero.loadouts[0].perkIDs, Is.Empty, "Removing 1 also drops 99, which no class perk has.");
    }
}
