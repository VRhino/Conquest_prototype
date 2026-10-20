using Unity.Mathematics;

/// <summary>
/// Contact distances derived from body radii and weapon reach (Movimiento_Formaciones.md L-7, C5).
/// <c>attackRange</c> is the distance from the attacker's origin to the far edge of its hitbox
/// (BraceWeaponProfiles), so the weapon reaches <c>attackRange − rcA</c> past the attacker's own body and the
/// hit lands while the target's body is within that edge: <c>dist ≤ attackRange + rcB</c>.
/// </summary>
public static class MeleeReach
{
    /// <summary>Extra clearance above touching bodies so a resting pair never overlaps.</summary>
    public const float BodyClearance = 0.05f;

    /// <summary>How far the weapon reaches beyond the attacker's own body (never negative).</summary>
    public static float Reach(float attackRange, float attackerRadius) => math.max(0f, attackRange - attackerRadius);

    /// <summary>Centre-to-centre distance at which a melee unit stops: bodies apart, target inside the hit.</summary>
    public static float StopDistance(float attackRange, float attackerRadius, float targetRadius, float reachFraction)
    {
        float touching = attackerRadius + targetRadius + BodyClearance;
        float stop = attackerRadius + targetRadius + math.saturate(reachFraction) * Reach(attackRange, attackerRadius);
        return math.min(math.max(stop, touching), ResumeDistance(attackRange, targetRadius));
    }

    /// <summary>Beyond this distance the unit approaches again (the hit no longer lands).</summary>
    public static float ResumeDistance(float attackRange, float targetRadius) => attackRange + targetRadius;

    /// <summary>The hitbox can reach the target's body.</summary>
    public static bool InAttackRange(float distance, float attackRange, float targetRadius) =>
        distance <= ResumeDistance(attackRange, targetRadius);

    /// <summary>
    /// Where a unit moving at <paramref name="velocity"/> comes to rest braking at <paramref name="deceleration"/>:
    /// v²/(2a) ahead, but never more than <paramref name="maxDistance"/> (do not run into the target). Stopping a
    /// moving unit "here" cut its velocity in one frame (M12).
    /// </summary>
    public static float2 BrakingStopPoint(float2 position, float2 velocity, float deceleration, float maxDistance)
    {
        float speedSq = math.lengthsq(velocity);
        if (deceleration <= 0f || speedSq < 1e-6f) return position;
        float distance = math.min(speedSq / (2f * deceleration), math.max(0f, maxDistance));
        return position + velocity * (distance / math.sqrt(speedSq));
    }

    /// <summary>Ranged units stop slightly inside their range and only approach again beyond it.</summary>
    public static float RangedStopDistance(float range, float stopFraction) => range * math.saturate(stopFraction);
}
