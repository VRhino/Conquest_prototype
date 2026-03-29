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
}
