using Unity.Mathematics;

/// <summary>
/// Rotation towards a target with a capped angular speed (Movimiento_Formaciones.md L5-4 / C7): a unit entering
/// combat turns to face its enemy over a few frames instead of snapping 180° in one.
/// </summary>
public static class UnitTurn
{
    /// <summary>Angle in radians between two rotations (0..π).</summary>
    public static float Angle(quaternion a, quaternion b)
    {
        float dot = math.abs(math.dot(math.normalize(a).value, math.normalize(b).value));
        return 2f * math.acos(math.min(1f, dot));
    }

    /// <summary>Rotates <paramref name="current"/> towards <paramref name="target"/> by at most maxDegreesPerSecond·dt.</summary>
    public static quaternion Step(quaternion current, quaternion target, float maxDegreesPerSecond, float dt)
    {
        float angle = Angle(current, target);
        float maxStep = math.radians(math.max(0f, maxDegreesPerSecond)) * math.max(0f, dt);
        if (angle <= maxStep || angle < 1e-5f) return math.normalize(target);
        return math.normalize(math.slerp(math.normalize(current), math.normalize(target), maxStep / angle));
    }
}
