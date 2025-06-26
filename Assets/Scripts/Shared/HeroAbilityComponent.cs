using Unity.Entities;

/// <summary>
/// Stores the ability references available to the hero. The
/// system expects three active abilities and one ultimate.
/// </summary>
public struct HeroAbilityComponent : IComponentData
{
    /// <summary>Ability triggered with the Q key.</summary>
    public Entity habilidad1;

    /// <summary>Ability triggered with the E key.</summary>
    public Entity habilidad2;

    /// <summary>Ability triggered with the R key.</summary>
    public Entity habilidad3;

    /// <summary>Ultimate ability triggered with the F key.</summary>
    public Entity ultimate;
}
