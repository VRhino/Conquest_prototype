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

    /// <summary>
    /// Health floor as a fraction of <see cref="maxHealth"/> (L1-07): a hit never takes the hero below it. Derived stat,
    /// written only by <see cref="StatResolveSystem"/>.
    /// </summary>
    public float minHealthRatio;
}
