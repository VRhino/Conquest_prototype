using Unity.Entities;

/// <summary>
/// Stores team information for an entity.
/// </summary>
public struct TeamComponent : IComponentData
{
    /// <summary>Team this entity belongs to.</summary>
    public Team value;
}
