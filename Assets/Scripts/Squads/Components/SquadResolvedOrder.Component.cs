using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Which system produced the winning order this frame.
/// </summary>
public enum OrderSource
{
    Player,
    AI,
    CombatReaction
}

/// <summary>
/// Written EXCLUSIVELY by OrderResolutionSystem.
/// Downstream systems (SquadOrderSystem, SquadFSMSystem) read only this component
/// to determine what the squad should do. Never written by any other system.
/// </summary>
public struct SquadResolvedOrderComponent : IComponentData
{
    /// <summary>Winning order type for this frame.</summary>
    public SquadOrderType order;

    /// <summary>Formation requested alongside the order.</summary>
    public FormationType formation;

    /// <summary>World position for Hold Position orders.</summary>
    public float3 holdPosition;

    /// <summary>Primary target entity (combat or movement target).</summary>
    public Entity targetEntity;

    /// <summary>Which system won the resolution this frame.</summary>
    public OrderSource source;

    /// <summary>True = a new order was issued this frame and SquadOrderSystem should process it.</summary>
    public bool hasNewOrder;
}
