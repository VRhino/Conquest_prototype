using Unity.Mathematics;

/// <summary>Shapes of skill effects on the ground plane: a circle, or a cone opening around the caster's facing.</summary>
public static class SkillArea
{
    /// <summary>Centre of an area effect: <paramref name="distance"/> in front of the caster.</summary>
    public static float3 Centre(float3 origin, float3 forward, float distance) => origin + forward * distance;

    /// <summary>
    /// Whether <paramref name="point"/> is within <paramref name="radius"/> of <paramref name="centre"/> and, for a cone
    /// (<paramref name="angleDegrees"/> between 0 and 360, exclusive), within half the angle of <paramref name="forward"/>.
    /// </summary>
    public static bool Contains(float3 centre, float3 forward, float radius, float angleDegrees, float3 point)
    {
        float2 offset = point.xz - centre.xz;
        float distSq = math.lengthsq(offset);
        if (distSq > radius * radius) return false;
        if (angleDegrees <= 0f || angleDegrees >= 360f || distSq < 1e-6f) return true;
        float2 facing = math.normalizesafe(forward.xz);
        float cos = math.dot(offset / math.sqrt(distSq), facing);
        return cos >= math.cos(math.radians(angleDegrees * 0.5f));
    }
}
