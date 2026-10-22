using System.Collections.Generic;

/// <summary>
/// Which perks a hero takes into battle (GDD 6.2: up to 5 passive and 2 active), from the ids chosen in its loadout,
/// among the perks its class allows. Unknown ids and perks past the limits are ignored, in loadout order.
/// </summary>
public static class PerkLoadout
{
    public const int MaxPassive = 5;
    public const int MaxActive = 2;

    public static void Select(IReadOnlyList<HeroPerk> available, IEnumerable<int> chosenIds, List<HeroPerk> passives, List<HeroPerk> actives)
    {
        passives.Clear();
        actives.Clear();
        if (available == null || chosenIds == null) return;
        foreach (int id in chosenIds)
        {
            HeroPerk perk = null;
            for (int i = 0; i < available.Count; i++)
                if (available[i] != null && available[i].perkID == id) { perk = available[i]; break; }
            if (perk == null || passives.Contains(perk) || actives.Contains(perk)) continue;
            if (perk.isPassive) { if (passives.Count < MaxPassive) passives.Add(perk); }
            else if (actives.Count < MaxActive) actives.Add(perk);
        }
    }

    /// <summary>Perk ids of the hero's active loadout (the first one if none is marked active); saved as text.</summary>
    public static List<int> ChosenIds(HeroData hero)
    {
        var ids = new List<int>();
        if (hero?.loadouts == null || hero.loadouts.Count == 0) return ids;
        var loadout = hero.loadouts.Find(l => l.isActive) ?? hero.loadouts[0];
        if (loadout?.perkIDs == null) return ids;
        foreach (var text in loadout.perkIDs)
            if (int.TryParse(text, out int id)) ids.Add(id);
        return ids;
    }
}
