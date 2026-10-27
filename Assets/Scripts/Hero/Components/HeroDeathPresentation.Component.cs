using Unity.Entities;

/// <summary>
/// What the visual layer must show while a hero is dead. Written only by HeroDeathPresentationSystem;
/// EntityVisualSync and RemoteHeroAnimationDriver read it for the Animator; HeroVisualInstantiationSystem
/// removes the visual while visualHidden and rebuilds it after the respawn.
/// </summary>
public struct HeroDeathPresentationComponent : IComponentData
{
    /// <summary>True from the death until the hero has respawned at its spawn pose (drives the Animator's IsDead).</summary>
    public bool isDead;

    /// <summary>Increments on every death; visuals fire the death animation when it changes.</summary>
    public uint deathRevision;

    /// <summary>Seconds since the current death. Zero while alive.</summary>
    public float timeSinceDeath;

    /// <summary>True from the hide delay after a death until the hero has respawned at its spawn pose: no visual in the scene.</summary>
    public bool visualHidden;
}
