using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Holds the current command issued to a squad.
/// Other systems will react to these values.
/// </summary>
public struct SquadInputComponent : IComponentData
{
    /// <summary>Requested order for the squad.</summary>
    public SquadOrderType orderType;

    /// <summary>True when a new order has been issued this frame.</summary>
    public bool hasNewOrder;

    /// <summary>Requested formation.</summary>
    public FormationType desiredFormation;

    /// <summary>
    /// Position for Hold Position orders. This is set when orderType is HoldPosition
    /// and represents the terrain position where the squad should hold formation.
    /// </summary>
    public float3 holdPosition;
}
