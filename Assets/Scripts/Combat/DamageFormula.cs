using Unity.Mathematics;

/// <summary>Constants of the damage formula (Docs/Mechanics/DamageMechanics.md §5.4), in one place.</summary>
public struct CombatTuning
{
    /// <summary>Fraction of each type's raw damage that always goes through.</summary>
    public float minDamageFraction;
    /// <summary>Attacker speed that gives the full kinetic bonus, m/s.</summary>
    public float kineticReferenceSpeed;
    /// <summary>Kinetic multiplier when the attacker has no weapon component.</summary>
    public float defaultKineticMultiplier;
    /// <summary>Height advantage above which the height bonus applies, m.</summary>
    public float heightBonusThreshold;
    /// <summary>Extra damage per metre of height advantage.</summary>
    public float heightBonusPerMetre;
    /// <summary>Lowest extra damage of a critical hit, whatever the defender's anti-critical (as in the reference).</summary>
    public float criticalExtraFloor;
    /// <summary>Lateral scatter of a shot at accuracy 0 (accuracy 100 = none).</summary>
    public float inaccuracySpread;
    /// <summary>Extra scatter at full speed, as a fraction of the standing one (the reference widens it when moving).</summary>
    public float movingSpreadFactor;
    /// <summary>Most projectiles in flight at once; past it the oldest is discarded (L2-7).</summary>
    public int maxLiveProjectiles;
    /// <summary>How far back a charge is measured, s: the run-up counted is between half this and this (L1-11).</summary>
    public float chargeWindowSeconds;

    public static CombatTuning Default => new CombatTuning
    {
        minDamageFraction = 0.05f,
        kineticReferenceSpeed = 5f,
        defaultKineticMultiplier = 0.3f,
        heightBonusThreshold = 0.5f,
        heightBonusPerMetre = 0.1f,
        criticalExtraFloor = 0.2f,
        inaccuracySpread = 0.15f,
        movingSpreadFactor = 1f,
        maxLiveProjectiles = 256,
        chargeWindowSeconds = 1f,
    };
}

/// <summary>Everything the formula needs, gathered by the caller (no entity lookups inside).</summary>
public struct DamageInput
{
    public DamageProfileComponent profile;
    /// <summary>Critical (or other) multiplier on the raw damage; 1 for a normal hit.</summary>
    public float multiplier;
    public DefenseComponent defense;
    /// <summary>Attacker's extra penetration (optional component; default = none).</summary>
    public PenetrationComponent penetration;
    public float attackerSpeed;
    /// <summary>The attacker weapon's kinetic multiplier, or a negative value to use the default.</summary>
    public float kineticMultiplier;
    /// <summary>Attacker height minus target height, m.</summary>
    public float heightDelta;
    /// <summary>Rolled damage variance (L1-12): the result is multiplied by 1 + this; 0 = none.</summary>
    public float varianceOffset;
    /// <summary>Sum of the ratios of the conditional modifiers that hold for this hit (L1-02); 0 = none.</summary>
    public float conditionalRatio;
    /// <summary>Metres the attacker came towards the target just before the hit (L1-11); the profile's traits rate it.</summary>
    public float chargeDistance;
}

/// <summary>Result with the breakdown of each stage, for tests and for showing it.</summary>
public struct DamageResult
{
    public float blunt, slashing, piercing;
    public float kineticBonus;
    public float heightBonus;
    /// <summary>Charge bonus of the weapon for the run-up (L1-11), 1 = none.</summary>
    public float chargeBonus;
    /// <summary>Random factor of the hit, 1 ± the weapon's variance (L1-12).</summary>
    public float varianceFactor;
    /// <summary>1 + the conditional ratio of the hit, never negative (L1-02).</summary>
    public float conditionalFactor;
    /// <summary>Final damage after the bonuses, never negative.</summary>
    public float effective;
}

/// <summary>
/// Damage formula as a pure function (Aprendizajes_Referencia.md L1-03): flat reduction per type with a floor,
/// then kinetic, height and charge (L1-11) bonuses, the rolled variance (L1-12) and the conditional modifiers' ratio (L1-02). Burst-friendly and testable without a world; shields and health are applied
/// by <see cref="DamageCalculationSystem"/>.
/// </summary>
public static class DamageFormula
{
    public static DamageResult Compute(in DamageInput input, in CombatTuning tuning)
    {
        var r = new DamageResult
        {
            blunt = TypeContribution(input.profile.bluntDamage, input.multiplier, input.defense.bluntDefense,
                input.profile.bluntPenetration + input.penetration.bluntPenetration, tuning.minDamageFraction),
            slashing = TypeContribution(input.profile.slashingDamage, input.multiplier, input.defense.slashDefense,
                input.profile.slashingPenetration + input.penetration.slashPenetration, tuning.minDamageFraction),
            piercing = TypeContribution(input.profile.piercingDamage, input.multiplier, input.defense.pierceDefense,
                input.profile.piercingPenetration + input.penetration.piercePenetration, tuning.minDamageFraction),
            kineticBonus = 1f,
            heightBonus = 1f,
            varianceFactor = math.max(0f, 1f + input.varianceOffset),
            conditionalFactor = math.max(0f, 1f + input.conditionalRatio),
            chargeBonus = ChargeTrack.Bonus(input.chargeDistance, input.profile.traits),
        };
        if (input.attackerSpeed > 0f && tuning.kineticReferenceSpeed > 0f)
        {
            float km = input.kineticMultiplier >= 0f ? input.kineticMultiplier : tuning.defaultKineticMultiplier;
            r.kineticBonus = 1f + input.attackerSpeed / tuning.kineticReferenceSpeed * km;
        }
        if (input.heightDelta > tuning.heightBonusThreshold)
            r.heightBonus = 1f + input.heightDelta * tuning.heightBonusPerMetre;
        r.effective = math.max(0f, (r.blunt + r.slashing + r.piercing) * r.kineticBonus * r.heightBonus * r.chargeBonus
            * r.varianceFactor * r.conditionalFactor);
        return r;
    }

    /// <summary>One type: max(raw − net defense, raw × floor); a type with no damage contributes nothing.</summary>
    public static float TypeContribution(float damage, float multiplier, float defense, float penetration, float floor)
    {
        if (damage <= 0f) return 0f;
        float raw = damage * multiplier;
        float netDefense = math.max(defense - penetration, 0f);
        return math.max(raw - netDefense, raw * floor);
    }
}
