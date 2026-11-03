using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>How a hit was delivered, for conditions (L1-02).</summary>
public enum HitForm : byte { Melee, Ranged, Ability }

/// <summary>Hit forms as flags (lists mean "any of"; none = any form).</summary>
[Flags]
public enum HitForms : byte
{
    None = 0,
    Melee = 1 << (int)HitForm.Melee,
    Ranged = 1 << (int)HitForm.Ranged,
    Ability = 1 << (int)HitForm.Ability,
}

/// <summary>Unit types as flags, one bit per <see cref="UnitType"/> (any of; none = any type).</summary>
[Flags]
public enum UnitTypes : byte
{
    None = 0,
    Infantry = 1 << (int)UnitType.Infantry,
    Cavalry = 1 << (int)UnitType.Cavalry,
    Distance = 1 << (int)UnitType.Distance,
}

/// <summary>Which side of the hit the owner of a conditional modifier must be on.</summary>
public enum HitSide : byte
{
    /// <summary>The owner lands the hit: "my damage +X % against ...".</summary>
    Attacker,
    /// <summary>The owner receives the hit: "damage I take −X % from ...".</summary>
    Defender,
}

/// <summary>
/// When a conditional modifier applies (Aprendizajes_Referencia.md L1-02; ref.: preconditions evaluated in the hit
/// context, on the attacker's and on the receiver's side). Every condition is combined with AND; lists mean "any of";
/// every default means "does not matter". "Other" is the other party of the hit: the target when the owner attacks,
/// the attacker when the owner defends.
/// </summary>
[Serializable]
public struct HitCondition
{
    public HitSide side;
    [Tooltip("Probabilidad (0-1) de que aplique en cada golpe, tirada sembrada; 0 = siempre.")]
    [Range(0f, 1f)] public float chance;
    [Tooltip("El otro es héroe o soldado.")]
    public PerkTargetKind otherKind;
    [Tooltip("El otro es un soldado de alguna de estas clases de escuadra; ninguna = cualquiera.")]
    public PerkSquadTypes otherSquadTypes;
    [Tooltip("El otro es un soldado de alguno de estos tipos de unidad; ninguno = cualquiera.")]
    public UnitTypes otherUnitTypes;
    [Tooltip("El golpe es de alguna de estas formas; ninguna = cualquiera.")]
    public HitForms forms;
    [Tooltip("El golpe sale de alguna de estas habilidades (ids); todo 0 = cualquiera.")]
    public int4 skillIds;
    [Tooltip("El golpe es (o no es) crítico.")]
    public PerkTristate critical;
    [Tooltip("El dueño tiene (o no) un efecto temporal activo en effectStat.")]
    public PerkTristate selfHasEffect;
    [Tooltip("El otro tiene (o no) un efecto temporal activo en effectStat.")]
    public PerkTristate otherHasEffect;
    public StatId effectStat;
    [Tooltip("Vida del que recibe el golpe, como fracción de su máximo: al menos / como mucho (máximo 0 = sin límite).")]
    [Range(0f, 1f)] public float defenderHealthMin;
    [Range(0f, 1f)] public float defenderHealthMax;
    [Tooltip("Armadura total del otro (suma de sus tres defensas): al menos / como mucho (0 = sin límite).")]
    public float otherArmorMin;
    public float otherArmorMax;
}

/// <summary>One party of a hit as the conditions see it, gathered by the damage system.</summary>
public struct HitParty
{
    public bool isHero;
    public bool isSoldier;
    public SquadType squadType;
    public UnitType unitType;
    /// <summary>Sum of its three defenses.</summary>
    public float totalArmor;
    /// <summary>Current health over max health, before this hit.</summary>
    public float healthFraction;
}

/// <summary>What the hit itself was.</summary>
public struct HitFacts
{
    public HitForm form;
    public int skillId;
    public bool critical;
}

/// <summary>
/// A modifier that only counts on the hits whose context meets its condition (L1-02): its ratio adds to the hit's
/// damage ratio, applied once as <c>× (1 + Σ ratios)</c> (T1 rule). Written by the owner of its source (squad data on
/// spawn, skills on cast); read only by <see cref="DamageCalculationSystem"/>.
/// </summary>
public struct ConditionalModifier : IBufferElementData
{
    public HitCondition condition;
    public float damageRatio;
    public ModifierSource source;
    public int sourceId;
    /// <summary>Simulation time at which it stops applying; 0 or less = permanent.</summary>
    public double expiresAt;
}

/// <summary>A conditional damage modifier as data (squad data, skill events).</summary>
[Serializable]
public struct ConditionalDamage
{
    public HitCondition condition;
    [Tooltip("Ratio de daño si se cumple la condición: +0,2 = +20 %; negativo en el lado receptor = recibe menos.")]
    public float damageRatio;
}

/// <summary>The pure rules of conditional modifiers (L1-02).</summary>
public static class HitConditions
{
    /// <summary>
    /// Whether <paramref name="c"/> holds. <paramref name="self"/> is the owner, <paramref name="other"/> the other party;
    /// the effect checks and the roll are gathered by the caller (only when the condition asks for them).
    /// </summary>
    public static bool Matches(in HitCondition c, in HitParty self, in HitParty other, in HitFacts facts,
        bool selfHasEffect, bool otherHasEffect, float roll)
    {
        if (c.chance > 0f && c.chance < 1f && roll >= c.chance) return false;
        if (c.otherKind == PerkTargetKind.Hero && !other.isHero) return false;
        if (c.otherKind == PerkTargetKind.Soldier && !other.isSoldier) return false;
        if (c.otherSquadTypes != PerkSquadTypes.None
            && (!other.isSoldier || (c.otherSquadTypes & (PerkSquadTypes)(1 << (int)other.squadType)) == 0)) return false;
        if (c.otherUnitTypes != UnitTypes.None
            && (!other.isSoldier || (c.otherUnitTypes & (UnitTypes)(1 << (int)other.unitType)) == 0)) return false;
        if (c.forms != HitForms.None && (c.forms & (HitForms)(1 << (int)facts.form)) == 0) return false;
        // Empty entries (0) are unused, so a hit without skill never matches a list.
        if (math.any(c.skillIds != 0) && !math.any((c.skillIds == facts.skillId) & (c.skillIds != 0))) return false;
        if (!Tristate(c.critical, facts.critical)) return false;
        if (!Tristate(c.selfHasEffect, selfHasEffect) || !Tristate(c.otherHasEffect, otherHasEffect)) return false;
        float defenderHealth = c.side == HitSide.Defender ? self.healthFraction : other.healthFraction;
        if (defenderHealth < c.defenderHealthMin) return false;
        if (c.defenderHealthMax > 0f && defenderHealth > c.defenderHealthMax) return false;
        if (c.otherArmorMin > 0f && other.totalArmor < c.otherArmorMin) return false;
        if (c.otherArmorMax > 0f && other.totalArmor > c.otherArmorMax) return false;
        return true;
    }

    public static bool Tristate(PerkTristate rule, bool value)
        => rule == PerkTristate.Any || (rule == PerkTristate.Must) == value;

    public static bool IsExpired(in ConditionalModifier m, double now) => m.expiresAt > 0 && m.expiresAt <= now;

    /// <summary>
    /// Replaces the modifiers of (<paramref name="source"/>, <paramref name="sourceId"/>) with <paramref name="added"/>
    /// and drops expired ones, so a recast refreshes instead of stacking.
    /// </summary>
    public static void Replace(DynamicBuffer<ConditionalModifier> modifiers, ModifierSource source, int sourceId,
        in ConditionalModifier added, double now)
    {
        for (int i = modifiers.Length - 1; i >= 0; i--)
        {
            var m = modifiers[i];
            if ((m.source == source && m.sourceId == sourceId) || IsExpired(m, now)) modifiers.RemoveAt(i);
        }
        modifiers.Add(added);
    }
}
