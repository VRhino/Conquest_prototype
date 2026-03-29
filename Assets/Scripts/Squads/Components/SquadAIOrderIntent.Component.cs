using Unity.Entities;

/// <summary>
/// Written by SquadAISystem each frame for squads owned by remote heroes (AI-controlled).
/// Carries the AI tactical intent and suggested order.
/// Read by OrderResolutionSystem.
/// </summary>
public struct SquadAIOrderIntentComponent : IComponentData
{
    /// <summary>Current tactical intent computed by SquadAISystem.</summary>
    public TacticalIntent tacticalIntent;

    /// <summary>Order the AI suggests for this squad.</summary>
    public SquadOrderType suggestedOrder;

    /// <summary>Primary target entity for the suggested order.</summary>
    public Entity targetEntity;
}
