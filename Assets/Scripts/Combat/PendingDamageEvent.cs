using Unity.Entities;

/// <summary>Which physical collider the attack contacted first.</summary>
public enum HitType : byte { Body = 0, Shield = 1 }

/// <summary>
/// Component used as an event to signal that an entity should receive damage.
/// The damage is applied by a separate system.
/// </summary>
public struct PendingDamageEvent : IComponentData
{
    /// <summary>True when the event owns its disposable entity (projectiles and melee contacts).</summary>
    public bool destroyEntityAfterProcessing;
    /// <summary>Entity that will receive the damage.</summary>
    public Entity target;

    /// <summary>Entity that originated the damage.</summary>
    public Entity damageSource;

    /// <summary>Damage profile to apply.</summary>
    public Entity damageProfile;

    /// <summary>Use the creation-time snapshot when the source profile may be retired.</summary>
    public bool hasInlineDamageProfile;
    public DamageProfileComponent inlineDamageProfile;

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

    /// <summary>
    /// Damage variance already rolled by whoever built the hit (L1-12, <see cref="CombatRandom.RollVariance"/>): the damage
    /// is multiplied by 1 + this. 0 = no variance.
    /// </summary>
    public float varianceOffset;

    /// <summary>How the hit was delivered, for conditional modifiers (L1-02). Melee by default.</summary>
    public HitForm hitForm;
    /// <summary>Skill the hit comes from (0 = none), for conditional modifiers (L1-02).</summary>
    public int skillId;

    /// <summary>Metres the attacker came towards the target just before a melee hit (L1-11, <see cref="ChargeTrack"/>).</summary>
    public float chargeDistance;

    /// <summary>A tick of an effect over time (L2-11): it comes from no direction, is never an ambush (L1-15).</summary>
    public bool periodic;
}
