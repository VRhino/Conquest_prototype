using Unity.Entities;

/// <summary>
/// Stores weapon parameters for a unit.
/// </summary>
public struct UnitWeaponComponent : IComponentData
{
    /// <summary>Entity holding the damage profile for this weapon.</summary>
    public Entity damageProfile;

    /// <summary>Maximum distance at which the attack is effective.</summary>
    public float attackRange;

    /// <summary>Time between consecutive attacks.</summary>
    public float attackInterval;

    /// <summary>
    /// Probability of landing a critical hit. Value between 0 and 1.
    /// Used by both units and the hero.
    /// </summary>
    public float criticalChance;

    /// <summary>Damage multiplier applied on a critical hit.</summary>
    public float criticalMultiplier;

    // ── Strike window timing ──────────────────────────────────────────────────
    /// <summary>Seconds from animation start when hitbox activates.</summary>
    public float strikeWindowStart;

    /// <summary>Duration the hitbox stays active (typically 0.1–0.2 s).</summary>
    public float strikeWindowDuration;

    /// <summary>Total duration of the attack animation.</summary>
    public float attackAnimationDuration;

    // ── Kinetic bonus ─────────────────────────────────────────────────────────
    /// <summary>Scales the penetration bonus earned from attacker speed.</summary>
    public float kineticMultiplier;
}
