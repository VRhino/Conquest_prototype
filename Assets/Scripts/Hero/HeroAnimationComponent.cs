using Unity.Entities;

/// <summary>
/// Holds animation triggers for the hero. Systems controlling visuals
/// can read these values to play the correct animations.
/// </summary>
public struct HeroAnimationComponent : IComponentData
{
    /// <summary>Set when a melee attack animation should play.</summary>
    public bool triggerAttack;
}
