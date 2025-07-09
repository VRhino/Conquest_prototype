using Unity.Entities;

/// <summary>
/// Links a squad entity to the hero that controls it.
/// </summary>
public struct SquadOwnerComponent : IComponentData
{
    /// <summary>Hero entity that owns this squad.</summary>
    public Entity hero;
}
