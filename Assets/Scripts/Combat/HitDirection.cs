using System;
using Unity.Mathematics;
using UnityEngine;

/// <summary>Where a hit comes from, seen from the target's front (Aprendizajes_Referencia.md L1-15).</summary>
public enum HitDirection : byte { Front, Flank, Back }

/// <summary>Hit directions as flags, one bit per <see cref="HitDirection"/> (any of; none = any direction).</summary>
[Flags]
public enum HitDirections : byte
{
    None = 0,
    Front = 1 << (int)HitDirection.Front,
    Flank = 1 << (int)HitDirection.Flank,
    Back = 1 << (int)HitDirection.Back,
}

/// <summary>Angles and damage bonuses of flank, back and ambush hits (L1-15), in <see cref="SquadSpawnConfigComponent"/>.</summary>
[Serializable]
public struct HitDirectionTuning
{
    [Tooltip("Ángulo entre el frente del objetivo y la dirección hacia el atacante a partir del cual el golpe es de flanco, grados.")]
    [Range(0f, 180f)] public float flankAngle;
    [Tooltip("Ángulo a partir del cual el golpe es por la espalda, grados.")]
    [Range(0f, 180f)] public float backAngle;
    [Tooltip("Daño extra de un golpe de flanco, como ratio: 0,15 = +15 %.")]
    public float flankDamageRatio;
    [Tooltip("Daño extra de un golpe por la espalda, como ratio.")]
    public float backDamageRatio;
    [Tooltip("Daño extra de una emboscada (la escuadra golpeada tiene al atacante en rango y no lo ve), como ratio; " +
             "se suma al de la dirección.")]
    public float ambushDamageRatio;

    public static HitDirectionTuning Default => new()
    {
        flankAngle = 60f,
        backAngle = 120f,
        flankDamageRatio = 0.15f,
        backDamageRatio = 0.3f,
        ambushDamageRatio = 0.25f,
    };

    /// <summary>Default values when the baked config predates these fields (all zero).</summary>
    public HitDirectionTuning OrDefault() => backAngle > 0f ? this : Default;
}

/// <summary>
/// Pure rules of the hit direction (L1-15; ref.: the hit class and the ambush flag are set before the damage chain, and
/// their bonuses add up into one ratio).
/// </summary>
public static class HitDirectionRules
{
    /// <summary>Below this squared length, in m², a direction is undefined and the hit counts as frontal.</summary>
    const float MinLengthSq = 1e-6f;

    /// <summary>
    /// The direction of a hit on a target at <paramref name="targetPosition"/> facing <paramref name="targetForward"/>,
    /// from an attacker at <paramref name="attackerPosition"/>; on the ground plane.
    /// </summary>
    public static HitDirection Classify(float3 targetForward, float3 targetPosition, float3 attackerPosition,
        in HitDirectionTuning tuning)
    {
        float2 toAttacker = attackerPosition.xz - targetPosition.xz;
        float2 forward = targetForward.xz;
        if (math.lengthsq(toAttacker) < MinLengthSq || math.lengthsq(forward) < MinLengthSq) return HitDirection.Front;
        float cos = math.clamp(math.dot(math.normalize(toAttacker), math.normalize(forward)), -1f, 1f);
        float angle = math.degrees(math.acos(cos));
        if (angle > tuning.backAngle) return HitDirection.Back;
        if (angle > tuning.flankAngle) return HitDirection.Flank;
        return HitDirection.Front;
    }

    /// <summary>Damage ratio of the hit: its direction's plus the ambush's, added (T1 rule).</summary>
    public static float DamageRatio(HitDirection direction, bool ambush, in HitDirectionTuning tuning)
    {
        float ratio = direction switch
        {
            HitDirection.Flank => tuning.flankDamageRatio,
            HitDirection.Back => tuning.backDamageRatio,
            _ => 0f,
        };
        return ratio + (ambush ? tuning.ambushDamageRatio : 0f);
    }

    /// <summary>Whether <paramref name="direction"/> is one of <paramref name="directions"/>; none = any.</summary>
    public static bool Allows(HitDirections directions, HitDirection direction)
        => directions == HitDirections.None || (directions & (HitDirections)(1 << (int)direction)) != 0;
}
