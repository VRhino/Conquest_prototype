using Unity.Entities;

/// <summary>
/// Stores cooldown timing information for a skill or ability.
/// </summary>
public struct CooldownComponent : IComponentData
{
    /// <summary>Base duration of the cooldown in seconds.</summary>
    public float cooldownDuration;

    /// <summary>Current remaining time until the ability can be used again.</summary>
    public float currentCooldown;

    /// <summary>True if the ability is ready for use.</summary>
    public bool isReady;
}
