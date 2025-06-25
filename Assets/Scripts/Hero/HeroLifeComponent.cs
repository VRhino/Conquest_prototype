using Unity.Entities;


/// <summary>
/// Tracks the alive/dead state of the hero.
/// </summary>
public struct HeroLifeComponent : IComponentData
{
    /// <summary>True if the hero can move and interact.</summary>
    public bool isAlive;

    /// <summary>Countdown timer while the hero is dead.</summary>
    public float deathTimer;

    /// <summary>Total time before the hero respawns.</summary>
    public float respawnDelay;
}
