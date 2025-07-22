using Unity.Entities;

/// <summary>
/// Stores the ability references available to the hero.
/// The component is populated by <see cref="HeroInitializationSystem"/>
/// when reading a <see cref="HeroClassDefinition"/> and is queried by
/// the <c>HeroAbilitySystem</c> as well as the UI to display ability
/// icons and cooldowns.
/// </summary>
public struct HeroAbilityComponent : IComponentData
{
    /// <summary>Ability triggered with the Q key.</summary>
    public Entity abilityQ;

    /// <summary>Ability triggered with the E key.</summary>
    public Entity abilityE;

    /// <summary>Ability triggered with the R key.</summary>
    public Entity abilityR;

    /// <summary>Ultimate ability triggered with the F key.</summary>
    public Entity ultimate;
}
