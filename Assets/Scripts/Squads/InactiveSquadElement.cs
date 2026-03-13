using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Buffer element on the hero entity storing the state of each inactive squad.
/// Used to track alive units between swaps and filter eliminated squads in UI.
/// </summary>
public struct InactiveSquadElement : IBufferElementData
{
    /// <summary>Int ID matching selectedSquads and SquadSwapRequest.newSquadId.</summary>
    public int squadId;

    /// <summary>Base squad string ID for looking up SquadDataIDComponent entity.</summary>
    public FixedString64Bytes baseSquadID;

    /// <summary>Number of alive units (full count if never deployed).</summary>
    public int aliveUnits;

    /// <summary>Original total units.</summary>
    public int totalUnits;

    /// <summary>True if all units are dead — squad cannot be re-invoked.</summary>
    public bool isEliminated;
}
