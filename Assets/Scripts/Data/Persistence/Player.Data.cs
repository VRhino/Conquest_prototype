using System;
using System.Collections.Generic;
using static DataCacheService;

/// <summary>
/// Root persistent profile for the local player.
/// Contains dynamic values such as owned heroes and account progression.
/// ScriptableObject references are represented by string identifiers.
/// </summary>
[Serializable]
public class PlayerData
{
    /// <summary>Runtime provenance: generated IDs must be saved before addressed rewards can be committed.</summary>
    [NonSerialized] public bool requiresIdentitySave;
    /// <summary>Zero denotes the original, unversioned profile.</summary>
    public int saveVersion;

    /// <summary>Legacy battle progression, preserved without guessing its hero owner.</summary>
    public LocalSaveSystem.PlayerProgressData battleProgress;

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

    /// <summary>
    /// Limpia los datos cacheados relacionados con el jugador y sus héroes.
    /// </summary>
    public void ClearCachedAttributes()
    {
        DataCacheService.Clear();
    }
}
