using System;
using System.Collections.Generic;

/// <summary>
/// Root persistent profile for the local player.
/// Contains dynamic values such as owned heroes and account progression.
/// ScriptableObject references are represented by string identifiers.
/// </summary>
[Serializable]
public class PlayerData
{
    /// <summary>Display name chosen by the player.</summary>
    public string playerName = string.Empty;

    /// <summary>Overall account level accumulated across heroes.</summary>
    public int accountLevel = 1;

    /// <summary>Total account experience used to reach the next level.</summary>
    public int accountXP = 0;

    /// <summary>Current amount of in-game currency.</summary>
    public int gold = 0;

    /// <summary>All heroes created by the player.</summary>
    public List<HeroData> heroes = new();
}
