using Unity.Entities;

/// <summary>
/// Singleton ECS component that holds hero gameplay tuning values.
/// Baked from HeroGameplayConfigAuthoring. Read by HeroAttackSystem and HeroStaminaSystem.
/// </summary>
public struct HeroGameplayConfigComponent : IComponentData
{
    public float attackStaminaCost;
    public float attackCooldown;
    public float criticalDamageMultiplier;

    public float sprintStaminaCostPerSecond;
    public float skill1StaminaCost;
    public float skill2StaminaCost;
    public float ultimateStaminaCost;

    /// <summary>Fraction of maxStamina above which exhaustion is cleared (e.g. 0.2 = 20%).</summary>
    public float exhaustionRecoveryThreshold;
}
