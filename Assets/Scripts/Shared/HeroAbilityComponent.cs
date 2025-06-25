using Unity.Entities;

/// <summary>
/// References data required to execute a hero ability.
/// </summary>
public struct HeroAbilityComponent : IComponentData
{
    /// <summary>Entity holding additional ability data or effects.</summary>
    public Entity abilityData;

    /// <summary>Stamina cost required to trigger this ability.</summary>
    public float staminaCost;

    /// <summary>Entity with the <see cref="CooldownComponent"/> that tracks readiness.</summary>
    public Entity cooldownEntity;
}
