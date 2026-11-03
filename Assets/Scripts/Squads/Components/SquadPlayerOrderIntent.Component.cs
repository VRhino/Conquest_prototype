using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Written by SquadControlSystem each frame for squads owned by the local player.
/// Carries the player's order intent plus heroOrdenCooldown state.
/// Read by OrderResolutionSystem to decide the final squad order.
/// </summary>
public struct SquadPlayerOrderIntentComponent : IComponentData
{
    /// <summary>Desired order type from player input this frame.</summary>
    public SquadOrderType orderType;

    /// <summary>Desired formation change, if any.</summary>
    public FormationType desiredFormation;

    /// <summary>World position for Hold Position orders.</summary>
    public float3 holdPosition;

    // ── Insistence (L3-05: absolute simulation times, nothing counts down per frame) ──

    /// <summary>How many times the same order has been pressed within the insistence window.</summary>
    public int insistenceCount;

    /// <summary>Simulation time at which the current insistence window closes.</summary>
    public double insistenceEndsAt;

    /// <summary>
    /// Simulation time until which the player's order overrides the combat reaction (the player insisted, or ordered
    /// FollowHero); 0 or past = no override. <see cref="OrderResolutionSystem"/> publishes it as the resolved order's
    /// <see cref="SquadResolvedOrderComponent.expiresAt"/>.
    /// </summary>
    public double overrideEndsAt;
}
