using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Buffer element on the DataContainer entity that maps int squad IDs
/// (from selectedSquads) to their base string IDs (for SquadDataIDComponent lookup).
/// Populated during battle scene setup.
/// </summary>
public struct SquadIdMapElement : IBufferElementData
{
    /// <summary>Int squad ID from the loadout.</summary>
    public int squadId;

    /// <summary>Base squad string ID for ECS lookup.</summary>
    public FixedString64Bytes baseSquadID;
}
