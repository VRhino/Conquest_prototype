using Unity.Entities;

/// <summary>
/// Transient player controls shared with movement. Persistent orders and
/// formation requests live in the intent and resolved-order components.
/// </summary>
public struct SquadInputComponent : IComponentData
{
    /// <summary>True when a new order has been issued this frame.</summary>
    public bool hasNewOrder;

    /// <summary>
    /// Si es true, el squad debe moverse rápidamente hacia el comandante (tecla C sostenida)
    /// </summary>
    public bool hurryToComander;
}
