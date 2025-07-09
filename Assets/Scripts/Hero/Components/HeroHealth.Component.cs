using Unity.Entities;

/// <summary>
/// Stores current and maximum health values for the hero.
/// </summary>
public struct HeroHealthComponent : IComponentData
{
    /// <summary>Current health points.</summary>
    public float currentHealth;

    /// <summary>Maximum health the hero can have.</summary>
    public float maxHealth;
}
