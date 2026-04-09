using Unity.Entities;

/// <summary>
/// Holds animation triggers for the hero. Systems controlling visuals
/// can read these values to play the correct animations.
/// </summary>
public struct HeroAnimationComponent : IComponentData
{
    /// <summary>Set when a melee attack animation should play.</summary>
    public bool triggerAttack;

    /// <summary>
    /// Head look X written by SamplePlayerAnimationController_ECS (local hero).
    /// Read by EntityVisualSync to drive upper body on remote heroes (BUG-002).
    /// </summary>
    public float headLookX;

    /// <summary>Head look Y, same as headLookX.</summary>
    public float headLookY;
}
