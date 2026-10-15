using System;
using System.Collections.Generic;
using UnityEngine;
using Data.Maps;
/// <summary>
/// Data structure representing a battle between attackers and defenders.
/// Contains identifiers for all participants on both sides of the conflict.
/// </summary>
[Serializable]
public class BattleData
{
    public MapDataSO mapData = null;
    /// <summary>List of attacker identifiers participating in the battle.</summary>
    public List<BattleHeroData> attackers = new();
    /// <summary>List of defender identifiers participating in the battle.</summary>
    public List<BattleHeroData> defenders = new();
    public string battleID = "";
    public int PreparationTimer = 60; //seconds
    public int BattleTimer = 900; // seconds (15 min default base timer)


    public Side playerSide(string heroName)
    {
        if (attackers.Exists(hero => hero.heroName == heroName)) return Side.Attackers;
        if (defenders.Exists(hero => hero.heroName == heroName)) return Side.Defenders;
        return Side.None;
    }
    /// <summary>
    /// Clears all participants from the battle.
    /// </summary>
    public void ClearAll()
    {
        attackers.Clear();
        defenders.Clear();
    }
    public BattleHeroData FindHero(HeroData hero)
        => hero == null ? null : FindHero(hero.heroId, hero.heroName);

    public BattleHeroData FindHero(string persistentId, string legacyName)
    {
        BattleHeroData found = null;
        foreach (var candidate in attackers)
            if (!string.IsNullOrEmpty(persistentId) && candidate.heroId == persistentId)
            {
                if (found != null) return null;
                found = candidate;
            }
        foreach (var candidate in defenders)
            if (!string.IsNullOrEmpty(persistentId) && candidate.heroId == persistentId)
            {
                if (found != null) return null;
                found = candidate;
            }
        if (found != null) return found;
        if (string.IsNullOrEmpty(legacyName)) return null;
        // Only legacy DTOs without identity may fall back to a name. Ambiguity is rejected.
        foreach (var candidate in attackers)
            if (candidate.heroName == legacyName && (string.IsNullOrEmpty(persistentId) || string.IsNullOrEmpty(candidate.heroId)))
            {
                if (found != null) return null;
                found = candidate;
            }
        foreach (var candidate in defenders)
            if (candidate.heroName == legacyName && (string.IsNullOrEmpty(persistentId) || string.IsNullOrEmpty(candidate.heroId)))
            {
                if (found != null) return null;
                found = candidate;
            }
        return found;
    }

    public Side GetSide(BattleHeroData hero)
        => hero == null ? Side.None : attackers.Contains(hero) ? Side.Attackers
            : defenders.Contains(hero) ? Side.Defenders : Side.None;

    public BattleHeroData findHeroDataByName(string heroName)
    {
        foreach (var hero in attackers)
        {
            if (hero.heroName == heroName) return hero;
        }
        foreach (var hero in defenders)
        {
            if (hero.heroName == heroName) return hero;
        }
        return null;
    }
}
