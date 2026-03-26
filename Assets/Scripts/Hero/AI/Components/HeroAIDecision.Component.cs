using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Output of the AI decision layer. Written every frame by exactly one behavior system.
/// Read by <see cref="HeroAIExecutionSystem"/> to produce movement, attack, and squad commands.
/// This is the stable interface between Decision and Execution — no downstream system
/// needs to know which behavior produced the decision.
/// </summary>
public struct HeroAIDecision : IComponentData
{
    // --- Action ---
    /// <summary>What the hero should do this frame.</summary>
    public AIActionType action;

    /// <summary>World position to move toward (used by MoveTo, CaptureZone, DefendZone, Retreat).</summary>
    public float3 targetPosition;

    /// <summary>Target entity (used by AttackTarget, CaptureZone, DefendZone).</summary>
    public Entity targetEntity;

    /// <summary>Whether the hero should sprint while moving.</summary>
    public bool shouldSprint;

    /// <summary>Whether the hero should trigger an attack this frame.</summary>
    public bool shouldAttack;

    // --- Squad Orders ---
    /// <summary>Order to issue to the hero's squad this frame.</summary>
    public SquadOrderType squadOrder;

    /// <summary>Hold position for HoldPosition orders.</summary>
    public float3 squadOrderPosition;

    /// <summary>True when a new squad order should be issued this frame.</summary>
    public bool hasNewSquadOrder;
}
