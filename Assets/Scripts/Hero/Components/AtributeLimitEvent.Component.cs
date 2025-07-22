using Unity.Entities;

/// <summary>
/// Event emitted when the player tries to assign a value outside the limits
/// allowed by their hero class.
/// </summary>
public struct AttributeLimitEvent : IComponentData
{
    /// <summary>Attribute that exceeded the allowed range.</summary>
    public HeroAttributeType attribute;

    /// <summary>Value that was attempted to be set.</summary>
    public int attemptedValue;
}
