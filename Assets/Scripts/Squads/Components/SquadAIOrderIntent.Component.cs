using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Written by HeroAIExecutionSystem for squads owned by remote heroes (AI-controlled).
/// Persists the requested order while combat reactions temporarily override it.
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

    /// <summary>World-space destination for HoldPosition.</summary>
    public float3 holdPosition;
}
