using System;
using System.Collections.Generic;
using UnityEditor;
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
