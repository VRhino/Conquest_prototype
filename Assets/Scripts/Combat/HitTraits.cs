using System;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// What a weapon's hit does besides its damage (Aprendizajes_Referencia.md L1-08, L1-10, L1-11, L1-12): declared by the weapon (squad
/// melee or ranged data, hero class) and carried inside <see cref="DamageProfileComponent"/>, so it travels with the
/// hit's inline profile. Every default is neutral, so data without traits behaves as before.
/// </summary>
[Serializable]
public struct HitTraits
{
    [Tooltip("El golpe no se bloquea: va a la vida aunque toque primero el escudo (L1-08).")]
    public bool ignoresShield;

    [Tooltip("Daño extra contra el bloqueo, como ratio: el escudo pierde el daño efectivo × (1 + valor). " +
             "0 = igual que a la vida; −1 = no lo gasta (L1-10).")]
    public float blockDamageRatio;

    [Tooltip("Aguante que pierde el héroe que bloquea, como fracción del daño efectivo del golpe (L1-10).")]
    public float staminaDamageRatio;

    [Tooltip("Variación aleatoria del daño: cada golpe se multiplica por un factor entre 1 − f y 1 + f, " +
             "tirado con el RNG sembrado del golpe (L1-12). 0 = siempre el mismo.")]
    [Range(0f, 1f)] public float damageVariance;

    [Tooltip("Carga (L1-11): metros recorridos hacia el objetivo justo antes del golpe por debajo de los cuales no hay bono.")]
    public float chargeThreshold;
    [Tooltip("Metros de carga con los que se alcanza el bono máximo (peso = min(d / esto, 1)).")]
    public float chargeFullDistance;
    [Tooltip("Bono máximo de daño de la carga, como ratio: 0,5 = ×1,5. 0 = el arma no carga.")]
    public float chargeMaxBonus;

    [Tooltip("Resiliencia que quita cada golpe que llega a la vida (L1-16), antes de los extras por crítico, dirección y " +
             "emboscada. 0 = el arma no aturde.")]
    [Min(0f)] public float resilienceDamage;

    [Tooltip("Atenuación por distancia (L1-17), solo en golpes a distancia: metros desde el punto de disparo con daño completo.")]
    [Min(0f)] public float falloffStart;
    [Tooltip("Metros a los que el daño llega a su mínimo; entre el inicio y esto baja en línea recta. " +
             "Igual o menor que el inicio = el arma no se atenúa.")]
    [Min(0f)] public float falloffEnd;
    [Tooltip("Fracción del daño que queda desde el final de la atenuación en adelante: 0,6 = 60 %.")]
    [Range(0f, 1f)] public float falloffMinFactor;

    [Tooltip("Clases de objetivo a las que el golpe no daña (L1-18): ni vida, ni resiliencia, ni retroceso.")]
    public HitTargets spares;
}

/// <summary>Target classes as flags, for immunities (L1-18). None = it damages everyone.</summary>
[Flags]
public enum HitTargets : byte
{
    None = 0,
    Heroes = 1 << 0,
    Soldiers = 1 << 1,
}

/// <summary>Pure rules of the per-weapon hit stages added for NvN (L1-17, L1-18).</summary>
public static class HitStages
{
    /// <summary>
    /// Damage kept by a ranged hit after <paramref name="distance"/> m (L1-17; ref.: the projectile's damage is attenuated
    /// before the penetration stage): 1 up to the start, linear down to the minimum at the end, the minimum beyond.
    /// </summary>
    public static float Falloff(float distance, in HitTraits traits)
    {
        if (traits.falloffEnd <= traits.falloffStart || distance <= traits.falloffStart) return 1f;
        float t = math.saturate((distance - traits.falloffStart) / (traits.falloffEnd - traits.falloffStart));
        return math.lerp(1f, math.saturate(traits.falloffMinFactor), t);
    }

    /// <summary>Whether a hit that spares <paramref name="spares"/> leaves this target unharmed (L1-18).</summary>
    public static bool Spares(HitTargets spares, bool targetIsHero, bool targetIsSoldier)
        => (targetIsHero && (spares & HitTargets.Heroes) != 0) || (targetIsSoldier && (spares & HitTargets.Soldiers) != 0);
}
