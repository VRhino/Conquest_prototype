using Unity.Mathematics;

/// <summary>
/// Critical hits as in the reference (Aprendizajes_Referencia.md L1-04, game-re notes/mechanics.md §2 and
/// damage-pipeline.md S2): the rate is <c>V / (P + V)</c> with the attacker's value and parameter (the defender does not
/// count), and the extra damage is <c>max(Vd / Pd − AV / AP + ratio, floor)</c> with the defender's anti-critical. The hit
/// multiplier is <c>1 + extra</c>.
/// </summary>
public static class CritFormula
{
    /// <summary>Parameter of the bases derived from a chance and a multiplier: the value then carries the number.</summary>
    public const float UnitParameter = 1f;

    /// <summary>Largest chance that still has a finite value (a chance of 1 would need an infinite one).</summary>
    const float MaxChance = 0.999f;

    /// <summary>Defender without anti-critical.</summary>
    public static CritStatsComponent NoAntiCrit => new CritStatsComponent { antiCritParameter = UnitParameter };

    public static float Rate(in CritStatsComponent attacker)
        => attacker.critValue <= 0f ? 0f : attacker.critValue / (math.max(0f, attacker.critParameter) + attacker.critValue);

    public static float Extra(in CritStatsComponent attacker, in CritStatsComponent defender, float floor)
    {
        float damage = attacker.critDamageParameter > 0f ? attacker.critDamageValue / attacker.critDamageParameter : 0f;
        float anti = defender.antiCritParameter > 0f ? defender.antiCritValue / defender.antiCritParameter : 0f;
        return math.max(damage - anti + attacker.critDamageRatio, floor);
    }

    public static float Multiplier(in CritStatsComponent attacker, in CritStatsComponent defender, float floor)
        => 1f + Extra(attacker, defender, floor);

    /// <summary>
    /// Critical value with <see cref="UnitParameter"/> that gives <paramref name="chance"/>: <c>c / (1 − c)</c>.
    /// Lets each weapon keep today's chance until equipment or perks add to the stat.
    /// </summary>
    public static float ValueForChance(float chance)
    {
        float c = math.clamp(chance, 0f, MaxChance);
        return c / (1f - c) * UnitParameter;
    }

    /// <summary>Critical-damage value with <see cref="UnitParameter"/> that gives <paramref name="multiplier"/>.</summary>
    public static float DamageValueForMultiplier(float multiplier) => math.max(0f, multiplier - 1f) * UnitParameter;
}
