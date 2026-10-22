using Unity.Mathematics;

/// <summary>
/// How a shot leaves the launcher (Aprendizajes_Referencia.md L2-7; ref.: a launcher scheme with mode, count and angle,
/// and a scatter that grows while moving): <see cref="FanDirection"/> spreads several projectiles evenly over an angle,
/// <see cref="Spread"/> is the random scatter of one shot.
/// </summary>
public static class LauncherScheme
{
    /// <summary>Direction of projectile <paramref name="index"/> of <paramref name="count"/>, rotated around Y over a fan.</summary>
    public static float3 FanDirection(float3 baseDirection, int index, int count, float fanAngleDegrees)
    {
        if (count <= 1 || fanAngleDegrees <= 0f) return baseDirection;
        float step = fanAngleDegrees / (count - 1);
        float angle = math.radians(-fanAngleDegrees * 0.5f + step * index);
        return math.mul(quaternion.RotateY(angle), baseDirection);
    }

    /// <summary>
    /// Scatter of a shot: <see cref="CombatTuning.inaccuracySpread"/> scaled by the missing accuracy (authored 1..100),
    /// widened by <see cref="CombatTuning.movingSpreadFactor"/> at full speed.
    /// </summary>
    public static float Spread(float accuracy, float speedFraction, in CombatTuning tuning)
        => (1f - math.saturate(accuracy / 100f)) * tuning.inaccuracySpread * (1f + tuning.movingSpreadFactor * math.saturate(speedFraction));
}
