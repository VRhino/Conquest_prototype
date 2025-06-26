using Unity.Entities;

/// <summary>
/// Stores stamina values and exhaustion state for the hero.
/// </summary>
public struct HeroStaminaComponent : IComponentData
{
    /// <summary>Current stamina value.</summary>
    public float currentStamina;

    /// <summary>Maximum stamina capacity.</summary>
    public float maxStamina;

    /// <summary>Regeneration rate in stamina per second.</summary>
    public float regenRate;

    /// <summary>True if stamina is depleted and abilities are locked.</summary>
    public bool isExhausted;
}
