using Unity.Mathematics;

/// <summary>
/// The physical-shield rule (Aprendizajes_Referencia.md L1-08, L1-10), as in the reference: the hit carries its own damage
/// against the block and against stamina, apart from its damage to health, and some hits go through the guard.
/// </summary>
public static class ShieldBlock
{
    /// <summary>
    /// The shield stops the hit: the weapon touched the shield first, the hit does not ignore it, and the shield has
    /// block left and is not broken.
    /// </summary>
    public static bool Blocks(in UnitShieldComponent shield, HitType hitType, in HitTraits traits)
        => hitType == HitType.Shield && !traits.ignoresShield && shield.currentBlock > 0f && shield.brokenTimer <= 0f;

    /// <summary>Block points the hit takes: the effective damage × (1 + the weapon's block ratio), never negative.</summary>
    public static float BlockDamage(float effectiveDamage, in HitTraits traits)
        => math.max(0f, effectiveDamage * (1f + traits.blockDamageRatio));

    /// <summary>Stamina a blocking hero loses: the effective damage × the weapon's stamina ratio, never negative.</summary>
    public static float StaminaDamage(float effectiveDamage, in HitTraits traits)
        => math.max(0f, effectiveDamage * traits.staminaDamageRatio);
}
