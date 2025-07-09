using Unity.Entities;

/// <summary>
/// Stores stamina values for the hero.
/// </summary>
public struct StaminaComponent : IComponentData
{
    /// <summary>Current stamina value.</summary>
    public float currentStamina;

    /// <summary>Maximum stamina the hero can reach.</summary>
    public float maxStamina;

    /// <summary>Regeneration rate in stamina per second.</summary>
    public float regenRate;

    /// <summary>Set to true when stamina is depleted.</summary>
    public bool isExhausted;
}
