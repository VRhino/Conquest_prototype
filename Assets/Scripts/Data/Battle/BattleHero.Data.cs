using System;
using System.Collections.Generic;

/// <summary>
/// Data structure representing a hero participant in a battle.
/// Contains essential information about the hero and their squads for battle scenarios.
/// </summary>
[Serializable]
public class BattleHeroData
{
    /// <summary>Name of the hero participating in the battle.</summary>
    public string heroName = string.Empty;

    /// <summary>Class identifier of the hero.</summary>
    public string classID = string.Empty;

    /// <summary>List of squad instance identifiers controlled by this hero.</summary>
    public List<SquadInstanceData> squadInstances = new();

    /// <summary>Current level of the hero.</summary>
    public int level = 1;
    /// <summary>Spawn point ID where the hero will enter the battle.</summary>
    public string spawnPointId = string.Empty;
}
