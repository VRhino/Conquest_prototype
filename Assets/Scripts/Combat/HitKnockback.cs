using Unity.Mathematics;

/// <summary>
/// Knock-back of a hit (pattern of the reference's <c>hit_curve</c>, game-re/notes/combat-timing-defense.md §4: the hit
/// moves the soldier along a curve over time). The curve is a smoothstep, so the push starts and ends at rest:
/// peak speed 1.5·D/T, peak acceleration 6·D/T².
/// </summary>
public static class HitKnockback
{
    /// <summary>Distance covered after <paramref name="elapsed"/> seconds of a push of <paramref name="distance"/> m.</summary>
    public static float Travelled(float elapsed, float duration, float distance)
    {
        if (duration <= 0f || distance <= 0f) return 0f;
        float u = math.saturate(elapsed / duration);
        return distance * u * u * (3f - 2f * u);
    }

    /// <summary>Distance to move this frame, from <paramref name="elapsed"/> to elapsed + dt.</summary>
    public static float Step(float elapsed, float deltaTime, float duration, float distance)
        => Travelled(elapsed + deltaTime, duration, distance) - Travelled(elapsed, duration, distance);

    /// <summary>Highest acceleration of the push: 6·D/T².</summary>
    public static float PeakAcceleration(float duration, float distance)
        => duration > 0f ? 6f * distance / (duration * duration) : float.PositiveInfinity;
}
