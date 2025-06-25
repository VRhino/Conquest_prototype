using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Stores player selection information that must persist across gameplay scenes.
/// </summary>
public struct DataContainerComponent : IComponentData
{
    /// <summary>Name chosen by the local player.</summary>
    public FixedString64Bytes playerName;

    /// <summary>Index of the squad currently selected.</summary>
    public int selectedSquad;

    /// <summary>List of perk identifiers selected by the player.</summary>
    public FixedList32Bytes<int> selectedPerks;

    /// <summary>Team identifier for this player.</summary>
    public int playerTeam;

    /// <summary>True when the player finished setting up and is ready.</summary>
    public bool isReady;
}
