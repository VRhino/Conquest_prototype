using Unity.Entities;

/// <summary>
/// Tracks health for an entity.
/// </summary>
public struct HealthComponent : IComponentData
{
    /// <summary>Maximum health value.</summary>
    public float maxHealth;

    /// <summary>Current health remaining.</summary>
    public float currentHealth;
}
