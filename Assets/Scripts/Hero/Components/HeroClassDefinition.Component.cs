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

    public int baseStrength;
    public int baseDexterity;
    public int baseArmor;
    public int baseVitality;

    public int minStrength;
    public int maxStrength;
    public int minDexterity;
    public int maxDexterity;
    public int minArmor;
    public int maxArmor;
    public int minVitality;
    public int maxVitality;

    /// <summary>Reference to the Q ability for this class.</summary>
    public Entity abilityQ;

    /// <summary>Reference to the E ability for this class.</summary>
    public Entity abilityE;

    /// <summary>Reference to the R ability for this class.</summary>
    public Entity abilityR;

    /// <summary>Reference to the ultimate ability (F key).</summary>
    public Entity ultimate;
}

/// <summary>
/// Buffer element used to store valid perk references for a hero class or hero.
/// </summary>
public struct ValidPerkElement : IBufferElementData
{
    public Entity Value;
}
