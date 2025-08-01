using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Stores player selection information that must persist across gameplay scenes.
/// </summary>
public struct DataContainerComponent : IComponentData
{
    /// <summary>Unique identifier for the local player.</summary>
    public int playerID;

    /// <summary>Name chosen by the local player.</summary>
    public FixedString64Bytes playerName;

    /// <summary>Team identifier for this player.</summary>
    public int teamID;

    /// <summary>Identifier of the loadout selected for the match.</summary>
    public int selectedLoadoutID;

    /// <summary>List of squad identifiers of the active loadout.</summary>
    public FixedList32Bytes<int> selectedSquads;

    /// <summary>List of perk identifiers selected by the player.</summary>
    public FixedList32Bytes<int> selectedPerks;

    /// <summary>Total leadership used by the selected loadout.</summary>
    public int totalLeadershipUsed;

    /// <summary>Identifier of the spawn point chosen during preparation.</summary>
    public int selectedSpawnID;

    /// <summary>True when the player finished setting up and is ready.</summary>
    public bool isReady;
}
