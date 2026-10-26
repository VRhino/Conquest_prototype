using Unity.Mathematics;

/// <summary>How a squad looks for new enemies (L4-09): it fixes the reference and the combat radius.</summary>
public enum SquadSearchStrategy
{
    /// <summary>Hold Position: defends its zone, smallest radius.</summary>
    Zone,
    /// <summary>Attack ordered by the player or the AI: attacks while advancing, largest radius.</summary>
    Advance,
    /// <summary>Following the hero or reacting to combat: the radius grows with the squad's speed.</summary>
    Follow,
}

/// <summary>Combat radius and terrain limits of the enemy search, baked from <see cref="MovementTuningComponent"/>.</summary>
public struct EnemySearchSettings
{
    public float radiusMin;          // Beyond the squad's own extent, m.
    public float radiusMax;
    public float fullRadiusSpeed;    // Squad speed at which Follow reaches radiusMax, m/s.
    public float maxHeightDifference;
    public float stepHeight;         // Height differences up to this ignore the slope limit, m.
    public float maxSlopeTan;        // tan of the steepest slope towards an enemy.

    public static EnemySearchSettings From(in MovementTuningComponent t) => new EnemySearchSettings
    {
        radiusMin = t.enemySearchRadiusMin,
        radiusMax = math.max(t.enemySearchRadiusMin, t.enemySearchRadiusMax),
        fullRadiusSpeed = t.enemySearchFullRadiusSpeed,
        maxHeightDifference = t.enemySearchMaxHeightDifference,
        stepHeight = t.enemySearchStepHeight,
        maxSlopeTan = math.tan(math.radians(math.clamp(t.enemySearchMaxSlopeDegrees, 0f, 89.9f))),
    };
}

/// <summary>
/// Squad enemy search (L4-09, pattern of the reference): each strategy fixes its reference and combat radius, the
/// radius grows with speed between a minimum and a maximum, and enemies at another level (height difference or a slope
/// too steep) are left out. Detection (EnemyDetectionSystem) stays the perception; this only decides which detected
/// enemies a squad may take on as new targets.
/// </summary>
public static class EnemySearch
{
    public static SquadSearchStrategy StrategyFor(SquadOrderType order, OrderSource source)
    {
        if (order == SquadOrderType.HoldPosition) return SquadSearchStrategy.Zone;
        if (order == SquadOrderType.Attack && source != OrderSource.CombatReaction) return SquadSearchStrategy.Advance;
        return SquadSearchStrategy.Follow;
    }

    /// <summary>Combat radius beyond the squad's extent for a strategy at <paramref name="speed"/> (m/s).</summary>
    public static float CombatRadius(SquadSearchStrategy strategy, float speed, in EnemySearchSettings s)
    {
        switch (strategy)
        {
            case SquadSearchStrategy.Zone: return s.radiusMin;
            case SquadSearchStrategy.Advance: return s.radiusMax;
            default:
                float t = s.fullRadiusSpeed > 0f ? math.saturate(speed / s.fullRadiusSpeed) : 1f;
                return math.lerp(s.radiusMin, s.radiusMax, t);
        }
    }

    /// <summary>
    /// The enemy is within <paramref name="radius"/> of the squad's extent: its distance to the reference minus
    /// <paramref name="squadRadius"/>, so a flank unit never ignores the enemy beside it.
    /// </summary>
    public static bool WithinRadius(float2 reference, float squadRadius, float2 enemy, float radius) =>
        math.distance(reference, enemy) - math.max(0f, squadRadius) <= radius;

    /// <summary>Same level: height difference within the limit, and a step or a slope gentle enough beyond it.</summary>
    public static bool SameLevel(float referenceY, float3 enemy, float horizontalDistance, in EnemySearchSettings s)
    {
        float dy = math.abs(enemy.y - referenceY);
        if (dy > s.maxHeightDifference) return false;
        if (dy <= s.stepHeight) return true;
        return dy <= math.max(0f, horizontalDistance) * s.maxSlopeTan;
    }
}
