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

    // ── heroOrdenCooldown state ───────────────────────────────────────────────

    /// <summary>How many times the same order has been pressed within the insistence window.</summary>
    public int insistenceCount;

    /// <summary>Time elapsed since the first press of the current insistence sequence.</summary>
    public float insistenceTimer;

    /// <summary>True = player insisted N times → movement overrides combat reaction.</summary>
    public bool heroOrdenCooldownActive;

    /// <summary>Remaining seconds of the heroOrdenCooldown.</summary>
    public float heroOrdenCooldownTimer;
}
