using Unity.Entities;

/// <summary>
/// Stable id of every stat the modifier stack knows (Aprendizajes_Referencia.md T1). One byte, never reordered:
/// it is the key for saves (L7-6) and the network. Append new stats before <see cref="Count"/>.
/// </summary>
public enum StatId : byte
{
    MaxHealth,
    MaxStamina,
    BluntDefense,
    SlashDefense,
    PierceDefense,
    BluntDamage,
    SlashDamage,
    PierceDamage,
    BluntPenetration,
    SlashPenetration,
    PiercePenetration,
    MoveSpeed,
    /// <summary>Critical rate value V; rate = V / (P + V) (reference pattern, L1-04).</summary>
    CritValue,
    /// <summary>Critical rate parameter P.</summary>
    CritParameter,
    /// <summary>Critical damage value Vd; extra = Vd / Pd − AV / AP + ratio.</summary>
    CritDamageValue,
    CritDamageParameter,
    /// <summary>Base extra damage of a critical (multiplier − 1).</summary>
    CritDamageRatio,
    /// <summary>Defender's anti-critical value AV (only reduces critical damage).</summary>
    AntiCritValue,
    AntiCritParameter,
    Strength,
    Dexterity,
    Armor,
    Vitality,
    /// <summary>Multiplier of every skill cooldown (base 1; a perk −0.2 ratio gives 0.8), capped by <see cref="CooldownUtility.MinScale"/>.</summary>
    CooldownScale,
    Count,
}

/// <summary>How a modifier combines: ratios add up and multiply the base once, flats add, an override wins.</summary>
public enum ModifierOp : byte { Ratio, Flat, Override }

/// <summary>Where a modifier comes from, so a source can remove exactly its own.</summary>
public enum ModifierSource : byte { Level, Equipment, Perk, Effect }

/// <summary>One modifier on one stat. Written by the system that owns its source; read only by StatResolveSystem.</summary>
public struct StatModifier : IBufferElementData
{
    public StatId stat;
    public ModifierOp op;
    public float value;
    public ModifierSource source;
    /// <summary>Id within the source (item, perk, effect) so it can be removed without touching others.</summary>
    public int sourceId;
    /// <summary>Simulation time at which it stops applying; 0 or less = permanent.</summary>
    public double expiresAt;
}

/// <summary>
/// Base value of each stat, indexed by <see cref="StatId"/> (buffer length = <see cref="StatId.Count"/>). Set once by
/// whoever spawns the entity; changes go through modifiers.
/// </summary>
public struct StatBaseElement : IBufferElementData
{
    public float value;
}

/// <summary>Enabled when the stats of the entity must be recomposed (base or modifiers changed).</summary>
public struct ModifiersDirty : IComponentData, IEnableableComponent { }

/// <summary>Earliest expiry among the entity's modifiers, so StatResolveSystem rechecks it only when due.</summary>
public struct StatModifierExpiry : IComponentData
{
    /// <summary>Simulation time of the next expiry; 0 = none.</summary>
    public double next;
}
