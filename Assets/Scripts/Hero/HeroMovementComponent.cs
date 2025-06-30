using Unity.Entities;

/// <summary>
/// Provides basic movement speed for the hero.
/// </summary>
public struct HeroMovementComponent : IComponentData
{
    /// <summary>World units moved per second when walking.</summary>
    public float movementSpeed;
}
