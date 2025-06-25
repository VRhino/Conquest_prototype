using Unity.Entities;

/// <summary>
/// Stores basic movement related stats for the hero.
/// </summary>
public struct HeroStatsComponent : IComponentData
{
    /// <summary>Base walking speed.</summary>
    public float baseSpeed;

    /// <summary>Multiplier applied when sprinting.</summary>
    public float sprintMultiplier;

    /// <summary>Force applied when jumping.</summary>
    public float jumpForce;
}
