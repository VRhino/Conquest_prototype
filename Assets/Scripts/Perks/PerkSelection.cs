using System.Collections.Generic;

/// <summary>
/// Choosing perks out of battle (H5, GDD 6.2): which of the class's perks the hero has unlocked, and adding or removing
/// one in the active loadout's <see cref="LoadoutSaveData.perkIDs"/> within the limits <see cref="PerkLoadout"/> takes
/// into battle (5 passive, 2 active). The talent tree and its points come later.
/// </summary>
public static class PerkSelection
{
    /// <summary>Perks of the class the hero may choose: unlocked by default or listed in its unlocked perks.</summary>
    public static List<HeroPerk> Available(IReadOnlyList<HeroPerk> classPerks, HeroData hero)
    {
        var available = new List<HeroPerk>();
        if (classPerks == null || hero == null) return available;
        foreach (var perk in classPerks)
            if (perk != null && (perk.isUnlockedByDefault || (hero.unlockedPerks != null && hero.unlockedPerks.Contains(perk.perkID))))
                available.Add(perk);
        return available;
    }

    /// <summary>The loadout <see cref="PerkLoadout.ChosenIds"/> reads (the active one, else the first); one is created if none.</summary>
    public static LoadoutSaveData ActiveLoadout(HeroData hero)
    {
        hero.loadouts ??= new List<LoadoutSaveData>();
        var loadout = hero.loadouts.Find(l => l.isActive) ?? (hero.loadouts.Count > 0 ? hero.loadouts[0] : null);
        if (loadout != null) return loadout;
        loadout = new LoadoutSaveData { name = "Loadout 1", isActive = true };
        hero.loadouts.Add(loadout);
        return loadout;
    }

    public static bool IsChosen(HeroData hero, HeroPerk perk) => PerkLoadout.ChosenIds(hero).Contains(perk.perkID);

    /// <summary>How many passive and active perks of <paramref name="available"/> the loadout holds.</summary>
    public static void Count(HeroData hero, IReadOnlyList<HeroPerk> available, out int passives, out int actives)
    {
        passives = actives = 0;
        var chosen = PerkLoadout.ChosenIds(hero);
        foreach (var perk in available)
        {
            if (!chosen.Contains(perk.perkID)) continue;
            if (perk.isPassive) passives++;
            else actives++;
        }
    }

    /// <summary>Whether <paramref name="perk"/> can be added: available and its kind not full.</summary>
    public static bool CanAdd(HeroData hero, IReadOnlyList<HeroPerk> available, HeroPerk perk)
    {
        if (perk == null || !Contains(available, perk) || IsChosen(hero, perk)) return false;
        Count(hero, available, out int passives, out int actives);
        return perk.isPassive ? passives < PerkLoadout.MaxPassive : actives < PerkLoadout.MaxActive;
    }

    /// <summary>
    /// Adds the perk to the active loadout or removes it. Ids the class no longer offers are dropped on the way, so the
    /// loadout never fills with perks the hero cannot take. Returns false when nothing changed (full, or not available).
    /// </summary>
    public static bool Toggle(HeroData hero, IReadOnlyList<HeroPerk> available, HeroPerk perk)
    {
        if (hero == null || perk == null) return false;
        bool chosen = IsChosen(hero, perk);
        if (!chosen && !CanAdd(hero, available, perk)) return false;
        var loadout = ActiveLoadout(hero);
        var ids = new List<string>();
        foreach (int id in PerkLoadout.ChosenIds(hero))
            if (id != perk.perkID && ContainsId(available, id)) ids.Add(id.ToString());
        if (!chosen) ids.Add(perk.perkID.ToString());
        loadout.perkIDs = ids;
        return true;
    }

    static bool Contains(IReadOnlyList<HeroPerk> perks, HeroPerk perk)
    {
        foreach (var p in perks) if (p == perk) return true;
        return false;
    }

    static bool ContainsId(IReadOnlyList<HeroPerk> perks, int id)
    {
        foreach (var p in perks) if (p != null && p.perkID == id) return true;
        return false;
    }
}
