using Unity.Entities;

/// <summary>
/// Stores combat related data for the hero such as the equipped weapon
/// and attack cooldown state.
/// </summary>
public struct HeroCombatComponent : IComponentData
{
    /// <summary>Entity representing the currently active weapon.</summary>
    public Entity activeWeapon;


    /// <summary>Set while the hero runs a skill (written by <see cref="SkillExecutionSystem"/>).</summary>
    public bool isAttacking;

    // ── Strike window lifecycle ──
    /// <summary>Prevents more than one hit per swing.</summary>
    public bool hitboxFired;

    // ── Configurable timing (initialized in baker) ──
    /// <summary>Seconds after attack start when the hitbox opens.</summary>
    public float strikeWindowStart;
    /// <summary>Duration in seconds the hitbox stays open.</summary>
    public float strikeWindowDuration;
    /// <summary>Full animation duration before isAttacking resets.</summary>
    public float attackAnimationDuration;
    /// <summary>Probability of landing a critical hit (0–1).</summary>
    public float criticalChance;
    /// <summary>Damage multiplier applied on a critical hit.</summary>
    public float criticalMultiplier;
}
