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
    /// <summary>Fraction of max health a hit cannot take a hero below (L1-07); 0 = none. Respawn protection gives it.</summary>
    MinHealthRatio,
    /// <summary>Fraction of the health a hit takes that heals the attacker (L1-13); 0 = none.</summary>
    LifeSteal,
    /// <summary>Multiplier of the squad's detection range (L4-10; base 1). Lives on the squad entity.</summary>
    AIDetectionRange,
    /// <summary>Multiplier of the squad's combat (enemy search) radius (L4-10; base 1).</summary>
    AICombatRadius,
    /// <summary>Added to the target-score bias towards enemy heroes (L4-10; base 0).</summary>
    AIHeroBias,
    /// <summary>Multiplier of the duration of the effects this entity applies (L2-10; base 1).</summary>
    EffectDurationOut,
    /// <summary>Multiplier of the duration of the effects this entity receives (L2-10; base 1).</summary>
    EffectDurationIn,
    /// <summary>Attack speed (L2-12; base 1): the phases marked to scale with it last duration / this.</summary>
    AttackSpeed,
    /// <summary>
    /// Most targets of the caster's skill events (L2-13). Not a stored stat: each event's own cap is the base, so a ratio
    /// scales every capped skill and a flat adds targets to it. Events without a cap stay uncapped. See <see cref="SkillEvents.Cap"/>.
    /// </summary>
    MaxTargets,
    /// <summary>Multiplier of the squad's running stamina cost (M18; base 1). Lives on the squad entity.</summary>
    StaminaCost,
    /// <summary>Most resilience of a soldier or hero (L1-16); 0 = it has none and is never stunned by hits.</summary>
    MaxResilience,
    /// <summary>Fraction of blunt damage taken off after the flat defense (L1-19; base 0), capped by <see cref="CombatTuning.maxRelief"/>.</summary>
    BluntRelief,
    SlashRelief,
    PierceRelief,
    Count,
}

/// <summary>How a modifier combines: ratios add up and multiply the base once, flats add, an override wins.</summary>
public enum ModifierOp : byte { Ratio, Flat, Override }

/// <summary>Where a modifier comes from, so a source can remove exactly its own.</summary>
public enum ModifierSource : byte
{
    Level, Equipment, Perk, Effect,
    /// <summary>Declared by the unit's squad type (squad data), e.g. its conditional modifiers (L1-02).</summary>
    UnitType,
    /// <summary>Declared by the squad type for the battle's terrain (L1-14).</summary>
    Terrain,
}

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
    /// <summary>Stacks of the effect (L2-10): the value counts this many times; 0 = 1, as before stacking existed.</summary>
    public byte stacks;
    /// <summary>Skill instance it is bound to (L2-14): removed when (<see cref="boundCaster"/>, <see cref="boundInstance"/>) stops running.</summary>
    public Entity boundCaster;
    public uint boundInstance;
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
