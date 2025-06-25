using Unity.Entities;

/// <summary>
/// Tracks the alive/dead state of the hero.
/// </summary>
public struct HeroLifeComponent : IComponentData
{
    /// <summary>True if the hero can move and interact.</summary>
    public bool isAlive;
}
