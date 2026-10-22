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

public static class PerkConditions
{
    public static bool Met(in PerkEffect effect, in HeroMotionComponent motion) => effect.condition switch
    {
        PerkCondition.HeroStill => motion.stillFor >= effect.conditionSeconds,
        PerkCondition.HeroMovedRecently => motion.sinceMoved <= effect.conditionSeconds,
        _ => true,
    };
}
