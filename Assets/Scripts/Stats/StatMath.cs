using Unity.Collections;
using Unity.Entities;

/// <summary>
/// The single combination rule of the modifier stack (T1): <c>(1 + Σ ratios) · base + Σ flats</c>; an override, if any,
/// wins (the last one added). Ratios add up and multiply once, so stacking never explodes multiplicatively.
/// Expired modifiers (expiresAt &gt; 0 and ≤ now) do not count.
/// </summary>
public static class StatMath
{
    public static float Resolve(float baseValue, NativeArray<StatModifier> modifiers, StatId stat, double now)
    {
        float ratio = 0f, flat = 0f, overrideValue = 0f;
        bool overridden = false;
        for (int i = 0; i < modifiers.Length; i++)
        {
            var m = modifiers[i];
            if (m.stat != stat || IsExpired(m, now)) continue;
            switch (m.op)
            {
                case ModifierOp.Ratio: ratio += m.value; break;
                case ModifierOp.Flat: flat += m.value; break;
                case ModifierOp.Override: overrideValue = m.value; overridden = true; break;
            }
        }
        return overridden ? overrideValue : (1f + ratio) * baseValue + flat;
    }

    public static bool IsExpired(in StatModifier m, double now) => m.expiresAt > 0 && m.expiresAt <= now;

    /// <summary>Earliest future expiry (0 = none), so the resolver knows when to look again.</summary>
    public static double NextExpiry(NativeArray<StatModifier> modifiers, double now)
    {
        double next = 0;
        for (int i = 0; i < modifiers.Length; i++)
        {
            double e = modifiers[i].expiresAt;
            if (e > now && (next == 0 || e < next)) next = e;
        }
        return next;
    }

    /// <summary>Base of <paramref name="stat"/>, or 0 when the buffer is shorter (older entity).</summary>
    public static float Base(DynamicBuffer<StatBaseElement> bases, StatId stat)
        => (int)stat < bases.Length ? bases[(int)stat].value : 0f;

    /// <summary>Removes every modifier of <paramref name="source"/> (and <paramref name="sourceId"/> when ≥ 0).</summary>
    public static int RemoveSource(DynamicBuffer<StatModifier> modifiers, ModifierSource source, int sourceId = -1)
    {
        int removed = 0;
        for (int i = modifiers.Length - 1; i >= 0; i--)
        {
            var m = modifiers[i];
            if (m.source != source || (sourceId >= 0 && m.sourceId != sourceId)) continue;
            modifiers.RemoveAt(i);
            removed++;
        }
        return removed;
    }
}
