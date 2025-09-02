using Unity.Entities;

/// <summary>
/// Holds the core attributes of the hero and a reference to the
/// <see cref="HeroClassDefinition"/> used during initialization.
/// </summary>
public struct HeroAttributesComponent : IComponentData
{
    /// <summary>Strength value of the hero.</summary>
    public int strength;

    /// <summary>Dexterity value of the hero.</summary>
    public int dexterity;

    /// <summary>Armor value of the hero.</summary>
    public int armor;

    /// <summary>Vitality (health pool modifier) of the hero.</summary>
    public int vitality;

    /// <summary>Entity representing the baked hero class definition.</summary>
    public Entity classDefinition;
}
