using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Recent positions of an attacker, so a melee hit knows how far it came towards its target (L1-11). Two samples spaced
/// half the charge window apart: the older one is between half and one window old. Written only by
/// <see cref="ChargeTrackSystem"/>.
/// </summary>
public struct ChargeTrackComponent : IComponentData
{
    public float3 older;
    public float3 newer;
    public double newerAt;
    public bool initialised;
}

/// <summary>
/// The charge rule (Aprendizajes_Referencia.md L1-11). Same structure as the reference (weapon threshold, weight
/// <c>min(d / X, 1)</c>, capped bonus), with <c>d</c> = metres the attacker came towards its target just before the
/// hit (user decision, 2026-11-03), on the ground plane.
/// </summary>
public static class ChargeTrack
{
    /// <summary>Moves the samples on once the newer one is half a window old.</summary>
    public static void Sample(ref ChargeTrackComponent track, float3 position, double now, float window)
    {
        if (!track.initialised)
        {
            track = new ChargeTrackComponent { older = position, newer = position, newerAt = now, initialised = true };
            return;
        }
        if (now - track.newerAt < window * 0.5f) return;
        track.older = track.newer;
        track.newer = position;
        track.newerAt = now;
    }

    /// <summary>Metres travelled from the older sample towards the target, along the line to it; never negative.</summary>
    public static float Approach(in ChargeTrackComponent track, float3 position, float3 target)
    {
        if (!track.initialised) return 0f;
        float2 toTarget = math.normalizesafe(target.xz - position.xz);
        return math.max(0f, math.dot(position.xz - track.older.xz, toTarget));
    }

    /// <summary>
    /// Damage factor of a charge of <paramref name="distance"/> metres: 1 under the weapon's threshold, else
    /// <c>1 + maxBonus · min(d / full, 1)</c>. A weapon without charge bonus never charges.
    /// </summary>
    public static float Bonus(float distance, in HitTraits traits)
    {
        if (traits.chargeMaxBonus <= 0f || distance <= traits.chargeThreshold) return 1f;
        float weight = traits.chargeFullDistance > 0f ? math.min(distance / traits.chargeFullDistance, 1f) : 1f;
        return 1f + traits.chargeMaxBonus * weight;
    }
}
