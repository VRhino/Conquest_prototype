using Unity.Entities;

/// <summary>
/// What the visual layer must show while a hero is dead. Written only by HeroDeathPresentationSystem;
/// EntityVisualSync, RemoteHeroAnimationDriver and HeroDeathVisualPresenter only read it.
/// </summary>
public struct HeroDeathPresentationComponent : IComponentData
{
    /// <summary>True from the death until the hero has respawned at its spawn pose (drives the Animator's IsDead).</summary>
    public bool isDead;

    /// <summary>Increments on every death; visuals fire the death animation when it changes.</summary>
    public uint deathRevision;

    /// <summary>Seconds since the current death. Zero while alive.</summary>
    public float timeSinceDeath;

    /// <summary>True from the hide delay after a death until the hero has respawned at its spawn pose.</summary>
    public bool visualHidden;
}
