using Unity.Entities;

/// <summary>
/// Singleton ECS component that holds hero gameplay tuning values.
/// Baked from HeroGameplayConfigAuthoring. Read by HeroInitializationSystem (basic attack), SkillExecutionSystem and HeroStaminaSystem.
/// </summary>
public struct HeroGameplayConfigComponent : IComponentData
{
    public float attackStaminaCost;
    public float attackCooldown;
    public float criticalDamageMultiplier;

    /// <summary>Hits in the basic attack chain (L2-3).</summary>
    public int basicComboLength;
    /// <summary>Seconds after a hit ends during which the next one of the chain is still accepted.</summary>
    public float comboChainWindow;
    /// <summary>Seconds a press turned down by a non-interruptible phase is remembered (input buffering).</summary>
    public float skillInputBuffer;

    public float sprintStaminaCostPerSecond;
    public float skill1StaminaCost;
    public float skill2StaminaCost;
    public float ultimateStaminaCost;

    /// <summary>Fraction of maxStamina above which exhaustion is cleared (e.g. 0.2 = 20%).</summary>
    public float exhaustionRecoveryThreshold;
}
