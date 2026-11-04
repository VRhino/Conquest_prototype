using Unity.Entities;

/// <summary>
/// Shield state for units carrying shields (e.g. Spearmen front row).
/// The shield collider (<see cref="ShieldHitboxBehaviour"/>) intercepts attacks before the hurtbox is hit, so where it
/// faces is where it is on the prefab. Regenerated over time by BlockRegenSystem.
/// </summary>
public struct UnitShieldComponent : IComponentData
{
    /// <summary>Current block value remaining.</summary>
    public float currentBlock;

    /// <summary>Maximum block capacity.</summary>
    public float maxBlock;

    /// <summary>Block points regenerated per second out of combat.</summary>
    public float regenRate;

    /// <summary>Seconds remaining in shield-break stun (>0 = broken: no block, no movement).</summary>
    public float brokenTimer;

    /// <summary>Stun duration in seconds when shield is fully depleted. Set at spawn from SquadData or config default.</summary>
    public float breakStunDuration;
}
