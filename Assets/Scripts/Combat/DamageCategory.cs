/// <summary>
/// Visual/logic category of damage for popup effects. New values go at the end: the FCT config stores them as numbers.
/// </summary>
public enum DamageCategory
{
    Normal,
    Critical,
    Ability,
    Blocked,
    Death,
    ShieldBreak,
    /// <summary>A plain hit from the target's flank (L1-15).</summary>
    Flank,
    /// <summary>A plain hit from behind (L1-15).</summary>
    Back,
    /// <summary>A plain hit from an attacker the target's squad could not see (L1-15).</summary>
    Ambush,
    /// <summary>The target's resilience broke and it is stunned (L1-16); no value.</summary>
    Stunned,
}

/// <summary>Which floating text a hit that reached health shows (C10).</summary>
public static class DamageFeedbackCategory
{
    /// <summary>
    /// Death, then critical, then ambush, back and flank; the latter only replace a plain hit, so a skill's hidden
    /// <see cref="DamageCategory.Ability"/> text stays hidden.
    /// </summary>
    public static DamageCategory Of(bool killed, bool critical, DamageCategory hitCategory, HitDirection direction, bool ambush)
    {
        if (killed) return DamageCategory.Death;
        if (critical) return DamageCategory.Critical;
        if (hitCategory != DamageCategory.Normal) return hitCategory;
        if (ambush) return DamageCategory.Ambush;
        return direction switch
        {
            HitDirection.Back => DamageCategory.Back,
            HitDirection.Flank => DamageCategory.Flank,
            _ => DamageCategory.Normal,
        };
    }
}
