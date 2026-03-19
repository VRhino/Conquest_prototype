using Unity.Entities;

/// <summary>
/// Stores per-unit combat data such as the current target and attack cooldown.
/// </summary>
public struct UnitCombatComponent : IComponentData
{
    /// <summary>Enemy entity currently targeted by this unit.</summary>
    public Entity target;

    /// <summary>Cooldown timer until the next attack can be executed.</summary>
    public float attackCooldown;

    /// <summary>Set when the unit is being flanked.</summary>
    public bool isFlanked;

    /// <summary>Set when the unit is being suppressed.</summary>
    public bool isSuppressed;

    // ── Attack animation state ────────────────────────────────────────────────
    /// <summary>True while the attack animation is playing.</summary>
    public bool isAttacking;

    /// <summary>Time elapsed since the current attack animation started.</summary>
    public float attackAnimationTimer;

    /// <summary>True once the hitbox has produced a damage event this swing.</summary>
    public bool hitboxFired;
}
