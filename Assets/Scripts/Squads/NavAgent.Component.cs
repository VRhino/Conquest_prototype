using Unity.Entities;

/// <summary>
/// Added to entities that have a <see cref="UnityEngine.AI.NavMeshAgent"/> linked via managed component.
/// </summary>
public struct NavAgentComponent : IComponentData
{
    /// <summary>
    /// True = NavMeshPositionSyncSystem owns the GO→ECS position sync each frame.
    /// EntityVisualSync skips its position write for this entity when this is true.
    /// </summary>
    public bool syncPositionFromNavMesh;

    /// <summary>Last raw formation slot evaluated by UnitNavMeshSystem.</summary>
    public Unity.Mathematics.float3 lastFormationRequest;

    /// <summary>Reachable projection used by arrival/state systems.</summary>
    public Unity.Mathematics.float3 effectiveFormationDestination;

    public Unity.Mathematics.float3 lastCommandDestination;
    public Unity.Mathematics.float3 lastFailedCommand;
    public bool hasEffectiveFormationDestination;
    public bool formationDestinationFailed;
    public bool hasIssuedCommand;
    public bool lastCommandWasFormation;
    public bool commandFailed;
    public bool hasProjectedCommand;
    public Unity.Mathematics.float3 lastProjectionRequest;
    public Unity.Mathematics.float3 projectedCommandDestination;
    public double nextProjectionRefreshTime;
    public double nextFormationRetryTime;
    public double nextCommandRetryTime;

    /// <summary>Requested slot of the previous frame and its smoothed horizontal velocity (destination lead).</summary>
    public Unity.Mathematics.float3 previousRequestedSlot;
    public Unity.Mathematics.float3 slotVelocity;
    public bool hasPreviousRequestedSlot;
}
