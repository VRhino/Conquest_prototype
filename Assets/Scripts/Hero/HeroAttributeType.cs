using Unity.Entities;

/// <summary>
/// Identifies a hero attribute validated by <see cref="HeroAttributeSystem"/>.
/// </summary>
public enum HeroAttributeType
{
    /// <summary>Strength attribute.</summary>
    Fuerza,
    /// <summary>Dexterity attribute.</summary>
    Destreza,
    /// <summary>Armor attribute.</summary>
    Armadura,
    /// <summary>Vitality attribute.</summary>
    Vitalidad
}

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

