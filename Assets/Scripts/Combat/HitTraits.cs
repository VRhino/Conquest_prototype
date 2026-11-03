using System;
using UnityEngine;

/// <summary>
/// What a weapon's hit does besides its damage (Aprendizajes_Referencia.md L1-08, L1-10, L1-12): declared by the weapon (squad
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
}
