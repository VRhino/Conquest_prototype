using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Runtime component baked from <see cref="HeroClassDefinition"/>.
/// Stores base attribute values, limits and references to abilities
/// and valid perks for a specific hero class.
/// </summary>
public struct HeroClassDefinitionComponent : IComponentData
{
    public HeroClass heroClass;

    public int baseFuerza;
    public int baseDestreza;
    public int baseArmadura;
    public int baseVitalidad;

    public int minFuerza;
    public int maxFuerza;
    public int minDestreza;
    public int maxDestreza;
    public int minArmadura;
    public int maxArmadura;
    public int minVitalidad;
    public int maxVitalidad;

    public Entity habilidad1;
    public Entity habilidad2;
    public Entity habilidad3;
    public Entity ultimate;
}

/// <summary>
/// Buffer element used to store valid perk references for a hero class or hero.
/// </summary>
public struct ValidPerkElement : IBufferElementData
{
    public Entity Value;
}
