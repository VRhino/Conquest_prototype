using Unity.Mathematics;

/// <summary>
/// Contact ring around a melee target (Movimiento_Formaciones.md L-7, C11): positions on a circle of radius
/// <c>d_parada</c>, one every <c>s_arco = 2·rc + m</c> of arc, at fixed world angles so they stay put while the
/// target turns. Attackers reserve one each; when the ring is full the rest wait further out (D6: no hard cap).
/// </summary>
public static class ContactRing
{
    /// <summary>The occupancy mask is a ulong.</summary>
    public const int MaxSlots = 64;

    /// <summary>Positions that fit on the ring: ⌊2π·r / s_arco⌋, at least 1.</summary>
    public static int SlotCount(float radius, float arcSpacing)
    {
        if (arcSpacing <= 0f) return 1;
        return math.clamp((int)math.floor(2f * math.PI * math.max(0f, radius) / arcSpacing), 1, MaxSlots);
    }

    /// <summary>World angle of a slot, radians counter-clockwise from +x.</summary>
    public static float SlotAngle(int slot, int count) => slot * (2f * math.PI / math.max(1, count));

    /// <summary>Slot position on the XZ plane.</summary>
    public static float2 SlotPosition(float2 target, int slot, int count, float radius)
    {
        math.sincos(SlotAngle(slot, count), out float s, out float c);
        return target + new float2(c, s) * radius;
    }

    /// <summary>Bearing of <paramref name="from"/> seen from <paramref name="target"/>, radians from +x.</summary>
    public static float Bearing(float2 target, float2 from)
    {
        float2 d = from - target;
        return math.lengthsq(d) < 1e-8f ? 0f : math.atan2(d.y, d.x);
    }

    /// <summary>
    /// Free slot whose angle is closest to <paramref name="bearing"/>, so the attacker keeps the side it comes
    /// from, and no further than <paramref name="maxArc"/> radians from it: slots round the back of the target
    /// lie among its own comrades. -1 when none is free there.
    /// </summary>
    public static int NearestFreeSlot(float bearing, int count, ulong occupied, float maxArc = math.PI)
    {
        count = math.clamp(count, 1, MaxSlots);
        float step = 2f * math.PI / count;
        int nearest = (int)math.round(bearing / step);
        int maxOffset = math.min(count / 2, (int)math.floor(math.max(0f, maxArc) / step + 1e-4f));
        for (int offset = 0; offset <= maxOffset; offset++)
        {
            // Closest first, alternating sides: nearest, +1, -1, +2, -2...
            int a = Wrap(nearest + offset, count);
            if ((occupied & (1UL << a)) == 0) return a;
            int b = Wrap(nearest - offset, count);
            if ((occupied & (1UL << b)) == 0) return b;
        }
        return -1;
    }

    /// <summary>Where an attacker without a slot waits: <c>d_reanudar + s_arco</c> out, on its own bearing.</summary>
    public static float WaitDistance(float resumeDistance, float arcSpacing) => resumeDistance + math.max(0f, arcSpacing);

    static int Wrap(int i, int count) => ((i % count) + count) % count;
}
