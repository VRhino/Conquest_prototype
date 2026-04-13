using Unity.Entities;

/// <summary>Which physical collider the attack contacted first.</summary>
public enum HitType : byte { Body = 0, Shield = 1 }

/// <summary>
/// Component used as an event to signal that an entity should receive damage.
/// The damage is applied by a separate system.
/// </summary>
public struct PendingDamageEvent : IComponentData
{
    /// <summary>Entity that will receive the damage.</summary>
    public Entity target;

    /// <summary>Entity that originated the damage.</summary>
    public Entity damageSource;

    /// <summary>Damage profile to apply.</summary>
    public Entity damageProfile;

    /// <summary>Team of the source entity to check friendly fire.</summary>
    public Team sourceTeam;

    /// <summary>Visual category for popup effects.</summary>
    public DamageCategory category;

    /// <summary>Damage multiplier (1 for normal, >1 for critical).</summary>
    public float multiplier;

    // ── Context for damage formula ────────────────────────────────────────────
    /// <summary>Attacker speed at the moment of impact (used for kinetic bonus).</summary>
    public float attackerSpeed;

    /// <summary>World position of the attacker (used for height bonus).</summary>
    public Unity.Mathematics.float3 attackerPosition;

    /// <summary>Which physical collider was contacted first (Body or Shield).</summary>
    public HitType hitType;
}
