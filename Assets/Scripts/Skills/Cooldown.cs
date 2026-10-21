using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// A cooldown group with absolute end time (Aprendizajes_Referencia.md L2-5; ref.: the clock lives per group, not per
/// skill, and group 0 has no cooldown). Absolute times need no per-frame system and are easy to reconcile with a server.
/// </summary>
public struct CooldownGroupElement : IBufferElementData
{
    public int groupId;
    /// <summary>Simulation time the group is ready again.</summary>
    public double endTime;
    /// <summary>Scaled duration of the last start, so the HUD can show a fraction.</summary>
    public float duration;
}

/// <summary>Cooldown multiplier resolved from the stack (<see cref="StatId.CooldownScale"/>), written by <see cref="StatResolveSystem"/>.</summary>
public struct CooldownScaleComponent : IComponentData
{
    public float value;
}

public static class CooldownUtility
{
    /// <summary>Lowest cooldown multiplier: reductions stop at half the duration (the reference caps it too).</summary>
    public const float MinScale = 0.5f;

    public static bool IsReady(DynamicBuffer<CooldownGroupElement> groups, int groupId, double now)
        => Remaining(groups, groupId, now) <= 0f;

    public static float Remaining(DynamicBuffer<CooldownGroupElement> groups, int groupId, double now)
    {
        if (groupId == 0) return 0f;
        for (int i = 0; i < groups.Length; i++)
            if (groups[i].groupId == groupId) return (float)math.max(0.0, groups[i].endTime - now);
        return 0f;
    }

    /// <summary>Fraction still to wait, 1 → 0 (0 when ready or never started).</summary>
    public static float Fraction(DynamicBuffer<CooldownGroupElement> groups, int groupId, double now)
    {
        for (int i = 0; i < groups.Length; i++)
            if (groups[i].groupId == groupId && groups[i].duration > 0f)
                return math.saturate((float)((groups[i].endTime - now) / groups[i].duration));
        return 0f;
    }

    /// <summary>Starts <paramref name="groupId"/> for <paramref name="duration"/> × <paramref name="scale"/> (capped).</summary>
    public static void Start(DynamicBuffer<CooldownGroupElement> groups, int groupId, float duration, float scale, double now)
    {
        if (groupId == 0 || duration <= 0f) return;
        float scaled = duration * math.max(MinScale, scale);
        var entry = new CooldownGroupElement { groupId = groupId, endTime = now + scaled, duration = scaled };
        for (int i = 0; i < groups.Length; i++)
            if (groups[i].groupId == groupId) { groups[i] = entry; return; }
        groups.Add(entry);
    }
}
