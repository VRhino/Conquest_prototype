using Unity.Mathematics;

/// <summary>
/// The absorb-shield rule (L1-06), as in the reference: the shield absorbs first and the excess passes to health; a hit
/// of 0 damage does not consume it; an expired shield absorbs nothing.
/// </summary>
public static class AbsorbShield
{
    public static bool IsActive(in AbsorbShieldComponent shield, double now)
        => shield.value > 0f && (shield.expiresAt <= 0 || shield.expiresAt > now);

    /// <summary>Takes <paramref name="damage"/> from the shield and returns what is left for health.</summary>
    public static float Absorb(ref AbsorbShieldComponent shield, float damage, double now)
    {
        if (damage <= 0f || !IsActive(shield, now)) return damage;
        float absorbed = math.min(shield.value, damage);
        shield.value -= absorbed;
        return damage - absorbed;
    }
}
