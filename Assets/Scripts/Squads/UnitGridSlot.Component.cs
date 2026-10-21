using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Slot of a unit in its squad formation. Single writer: <see cref="FormationSystem"/> (initial values from
/// <see cref="SquadSpawningSystem"/>). Readers rotate <see cref="localOffset"/> by the formation anchor.
/// </summary>
public struct UnitGridSlotComponent : IComponentData
{
    /// <summary>Slot index in the current template; stays with the unit when others die.</summary>
    public int slotIndex;
    /// <summary>Slot position in spacing units, without noise (+y = forward).</summary>
    public float2 localPosition;
    /// <summary>Rank from the front (0 = front row).</summary>
    public int row;
    public FormationSlotRole role;
    /// <summary>Local facing with noise, radians clockwise from the formation forward.</summary>
    public float headingLocal;
    /// <summary>Unrotated offset from the anchor in metres, with spacing and noise applied.</summary>
    public float3 localOffset;
}
