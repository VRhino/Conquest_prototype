using System;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>When a perk effect applies (Aprendizajes_Referencia.md L7-4).</summary>
public enum PerkTrigger : byte
{
    /// <summary>An aura: on while its condition holds.</summary>
    Always,
    /// <summary>On each hit the hero lands, to the target.</summary>
    OnHit,
}

/// <summary>Simple conditions of the reference's preconditions (game-re notes/preconditions.md), about the hero's motion.</summary>
public enum PerkCondition : byte
{
    None,
    /// <summary>The hero has not moved for <see cref="PerkEffect.conditionSeconds"/>.</summary>
    HeroStill,
    /// <summary>The hero moved within the last <see cref="PerkEffect.conditionSeconds"/>.</summary>
    HeroMovedRecently,
}

public enum PerkTarget : byte
{
    Self,
    OwnSquad,
    /// <summary>Allied heroes and units within <see cref="PerkEffect.radius"/>, the hero included.</summary>
    AlliesInRadius,
    /// <summary>The entity the hit landed on (<see cref="PerkTrigger.OnHit"/> only).</summary>
    HitTarget,
    /// <summary>Enemies within <see cref="PerkEffect.radius"/> of the hero (active perks).</summary>
    EnemiesInRadius,
}

/// <summary>One effect of a perk: a modifier on the stack, given when the trigger and the condition say so.</summary>
[Serializable]
public struct PerkEffect
{
    public PerkTrigger trigger;
    public PerkCondition condition;
    public float conditionSeconds;
    public PerkTarget target;
    public float radius;
    public StatId stat;
    public ModifierOp op;
    public float value;
    /// <summary>How long an on-hit effect lasts on the target.</summary>
    public float duration;

    // Richer conditions (L2-15), all combined with AND; every default means "does not matter", so older data is unchanged.
    /// <summary>Probability in (0, 1) that the effect applies each time; 0 (or 1 and above) = always.</summary>
    public float chance;
    public PerkTargetKind targetKind;
    /// <summary>Soldier classes the target may belong to (any of them); none = any class.</summary>
    public PerkSquadTypes targetSquadTypes;
    /// <summary>Whether the target must (or must not) carry an active timed effect on <see cref="effectStat"/>.</summary>
    public PerkTristate targetHasEffect;
    /// <summary>Whether the hero must (or must not) carry an active timed effect on <see cref="effectStat"/>.</summary>
    public PerkTristate selfHasEffect;
    public StatId effectStat;
    /// <summary>Fraction of the hero's squad still alive (0-1) at least / at most; a max of 0 = no upper limit.</summary>
    public float squadAliveMin;
    public float squadAliveMax;
}

/// <summary>What the target of a perk effect must be (L2-15).</summary>
public enum PerkTargetKind : byte { Any, Hero, Soldier }

/// <summary>"Must", "must not" or "does not matter" (L2-15, ref.: tri-state conditions).</summary>
public enum PerkTristate : byte { Any, Must, MustNot }

/// <summary>Soldier classes as flags, one bit per <see cref="SquadType"/> (L2-15: lists mean "any of").</summary>
[Flags]
public enum PerkSquadTypes : byte
{
    None = 0,
    Squires = 1 << (int)SquadType.Squires,
    Archers = 1 << (int)SquadType.Archers,
    Pikemen = 1 << (int)SquadType.Pikemen,
    Spearmen = 1 << (int)SquadType.Spearmen,
}

/// <summary>What the hero side of a perk condition looks at, gathered by the system that applies it.</summary>
public struct PerkSourceContext
{
    public HeroMotionComponent motion;
    /// <summary>Uniform roll in [0, 1) for the chance (<see cref="PerkConditions.Roll"/>).</summary>
    public float roll;
    public bool selfHasEffect;
    public bool hasSquad;
    public float squadAliveFraction;
}

/// <summary>What the target side of a perk condition looks at.</summary>
public struct PerkTargetContext
{
    public bool isHero;
    public bool isSoldier;
    public SquadType squadType;
    public bool hasEffect;
}

/// <summary>A perk effect the hero carries into battle (from its loadout), with the time of its next aura refresh.</summary>
public struct HeroPerkElement : IBufferElementData
{
    public int perkId;
    public PerkEffect effect;
    public double nextRefresh;
}

/// <summary>How long the hero has been still and since it last moved, for perk conditions.</summary>
public struct HeroMotionComponent : IComponentData
{
    public float3 lastPosition;
    public float stillFor;
    public float sinceMoved;
    public bool initialised;
}

/// <summary>
/// Perk conditions (L7-4, L2-15; ref.: conditions combine with AND, lists mean "any of", tri-states tell "does not
/// matter", "must" and "must not"). The hero side is checked once per effect; the target side per target.
/// </summary>
public static class PerkConditions
{
    /// <summary>Salt of the perk chance roll in <see cref="CombatRandom"/>.</summary>
    public const uint ChanceSalt = 0xc2b2ae35u;

    public static bool Met(in PerkEffect effect, in PerkSourceContext source, in PerkTargetContext target)
        => MetBySource(effect, source) && MetByTarget(effect, target);

    public static bool MetBySource(in PerkEffect effect, in PerkSourceContext source)
    {
        bool motion = effect.condition switch
        {
            PerkCondition.HeroStill => source.motion.stillFor >= effect.conditionSeconds,
            PerkCondition.HeroMovedRecently => source.motion.sinceMoved <= effect.conditionSeconds,
            _ => true,
        };
        if (!motion) return false;
        if (effect.chance > 0f && effect.chance < 1f && source.roll >= effect.chance) return false;
        if (!Tristate(effect.selfHasEffect, source.selfHasEffect)) return false;
        if (effect.squadAliveMin > 0f || effect.squadAliveMax > 0f)
        {
            if (!source.hasSquad || source.squadAliveFraction < effect.squadAliveMin) return false;
            if (effect.squadAliveMax > 0f && source.squadAliveFraction > effect.squadAliveMax) return false;
        }
        return true;
    }

    public static bool MetByTarget(in PerkEffect effect, in PerkTargetContext target)
    {
        if (effect.targetKind == PerkTargetKind.Hero && !target.isHero) return false;
        if (effect.targetKind == PerkTargetKind.Soldier && !target.isSoldier) return false;
        if (effect.targetSquadTypes != PerkSquadTypes.None
            && (!target.isSoldier || ((int)effect.targetSquadTypes & (1 << (int)target.squadType)) == 0)) return false;
        return Tristate(effect.targetHasEffect, target.hasEffect);
    }

    static bool Tristate(PerkTristate condition, bool value) => condition switch
    {
        PerkTristate.Must => value,
        PerkTristate.MustNot => !value,
        _ => true,
    };

    /// <summary>Seeded roll of one perk on one target at one tick (L1-05): the same in a replay, whatever the order.</summary>
    public static float Roll(uint tick, Entity source, Entity target, int perkId)
        => CombatRandom.ForHit(tick, source, math.hash(new uint3(ChanceSalt, (uint)perkId, (uint)target.Index))).NextFloat();

    /// <summary>Whether <paramref name="modifiers"/> hold an active timed effect (source <see cref="ModifierSource.Effect"/>) on <paramref name="stat"/>.</summary>
    public static bool HasActiveEffect(DynamicBuffer<StatModifier> modifiers, StatId stat, double now)
    {
        foreach (var m in modifiers)
            if (m.source == ModifierSource.Effect && m.stat == stat && m.expiresAt > now) return true;
        return false;
    }

    /// <summary>Whether the effect asks anything of its target, so auras can skip gathering it.</summary>
    public static bool NeedsTarget(in PerkEffect effect)
        => effect.targetKind != PerkTargetKind.Any || effect.targetSquadTypes != PerkSquadTypes.None
           || effect.targetHasEffect != PerkTristate.Any;
}
