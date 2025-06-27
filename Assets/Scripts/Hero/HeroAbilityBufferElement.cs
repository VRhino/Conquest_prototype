using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Buffer element storing hero ability parameters baked from <see cref="HeroAbilityData"/>.
/// </summary>
public struct HeroAbilityBufferElement : IBufferElementData
{
    /// <summary>Name of the ability.</summary>
    public FixedString64Bytes name;
    /// <summary>Cooldown time in seconds.</summary>
    public float cooldown;
    /// <summary>Stamina required to use the ability.</summary>
    public float staminaCost;
    /// <summary>Multiplier applied to base damage.</summary>
    public float damageMultiplier;
    /// <summary>Ability category mapped to input.</summary>
    public AbilityCategory category;
}
