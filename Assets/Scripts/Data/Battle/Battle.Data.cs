using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Data structure representing a battle between attackers and defenders.
/// Contains identifiers for all participants on both sides of the conflict.
/// </summary>
[Serializable]
public class BattleData
{
    public string mapName = "DefaultMap";
    /// <summary>List of attacker identifiers participating in the battle.</summary>
    public List<BattleHeroData> attackers = new();
    /// <summary>List of defender identifiers participating in the battle.</summary>
    public List<BattleHeroData> defenders = new();
    public string battleID = "";
    public int PreparationTimer = 60; //seconds

    /// <summary>
    /// Clears all participants from the battle.
    /// </summary>
    public void ClearAll()
    {
        attackers.Clear();
        defenders.Clear();
    }
}
