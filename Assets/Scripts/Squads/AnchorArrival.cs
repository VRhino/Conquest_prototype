using Unity.Mathematics;

/// <summary>
/// Arrival of the squad anchor (Movimiento_Formaciones.md L-1, C10 reduced by D5 to position only): the anchor
/// follows its target point with its own velocity instead of copying it, speeding up at most at
/// <c>acceleration</c> and braking in advance (v ≤ √(2·a·d), Reynolds' <i>arrive</i>) so it stops on the target
/// without overshooting. Slots move with the anchor, so the squad no longer starts or stops in a single frame
/// when the hero does. Works on the horizontal plane; height and rotation follow the target directly.
/// </summary>
public static class AnchorArrival
{
    /// <summary>Below this distance and speed the anchor settles exactly on the target.</summary>
    public const float SettleDistance = 0.01f;
    public const float SettleSpeed = 0.05f;

    /// <summary>
    /// Advances the anchor one step towards <paramref name="target"/>. <paramref name="acceleration"/> ≤ 0 copies
    /// the target (the old behaviour); beyond <paramref name="snapDistance"/> (spawn, respawn, teleport) it jumps.
    /// Braking may reach <paramref name="brakingFactor"/> × the acceleration, as for the units.
    /// </summary>
    public static float3 Step(float3 position, ref float3 velocity, float3 target, float acceleration,
        float brakingFactor, float snapDistance, float deltaTime)
    {
        float2 offset = target.xz - position.xz;
        float distance = math.length(offset);
        if (acceleration <= 0f || deltaTime <= 0f || distance > snapDistance)
        {
            velocity = float3.zero;
            return target;
        }

        float2 current = velocity.xz;
        // Desired speed: whatever still lets it stop on the target at the planned deceleration.
        float2 desired = distance > 1e-5f
            ? offset / distance * math.sqrt(2f * acceleration * distance)
            : float2.zero;
        float2 next = AllyAvoidance.LimitAcceleration(current, desired, acceleration * deltaTime,
            acceleration * math.max(1f, brakingFactor) * deltaTime);
        // Never step past the target.
        float2 step = next * deltaTime;
        if (math.lengthsq(step) >= distance * distance)
        {
            velocity = float3.zero;
            return target;
        }

        float2 xz = position.xz + step;
        if (math.distance(xz, target.xz) < SettleDistance && math.length(next) < SettleSpeed)
        {
            velocity = float3.zero;
            return target;
        }
        velocity = new float3(next.x, 0f, next.y);
        return new float3(xz.x, target.y, xz.y);
    }

    /// <summary>Distance the anchor trails a target moving steadily at <paramref name="speed"/>: v²/(2a).</summary>
    public static float SteadyLag(float speed, float acceleration)
        => acceleration > 0f ? speed * speed / (2f * acceleration) : 0f;
}
