using Unity.Entities;

/// <summary>
/// Stores a reference to the squad entity spawned for a hero.
/// </summary>
public struct HeroSquadReference : IComponentData
{
    /// <summary>Spawned squad entity.</summary>
    public Entity squad;
}
