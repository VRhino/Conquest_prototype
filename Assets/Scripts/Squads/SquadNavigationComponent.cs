using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Navigation data for a squad. Systems move the leader towards
/// <see cref="targetPosition"/> using Unity's NavMesh.
/// </summary>
public struct SquadNavigationComponent : IComponentData
{
    /// <summary>World position the squad should move to.</summary>
    public float3 targetPosition;

    /// <summary>True while the squad is navigating.</summary>
    public bool isNavigating;

    /// <summary>Distance threshold to consider the destination reached.</summary>
    public float arrivalThreshold;
}
