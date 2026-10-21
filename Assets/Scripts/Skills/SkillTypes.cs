using System;
using Unity.Entities;

/// <summary>Input slot a hero skill is launched from (Aprendizajes_Referencia.md L2-1).</summary>
public enum SkillSlot : byte
{
    Basic,
    Q,
    E,
    R,
    Ultimate,
    ActivePerk1,
    ActivePerk2,
}

/// <summary>
/// What a new skill does with the current one (game-re notes/skill-lifecycle.md §3.2): <see cref="Interrupt"/> replaces
/// it; <see cref="Yield"/> parks it and restores it when it ends, unless the current one is already in its final phase.
/// </summary>
public enum SkillRefresh : byte
{
    Interrupt,
    Yield,
}

public enum SkillEventType : byte
{
    /// <summary>Plays the hero's attack animation (placeholder until each skill has its own).</summary>
    Animation,
    HitboxOpen,
    HitboxClose,
    /// <summary>Damage in a circle or cone in front of the hero (<see cref="SkillEvent.radius"/>, <see cref="SkillEvent.angle"/>).</summary>
    AreaDamage,
    /// <summary>Timed modifier on the stack of <see cref="SkillEvent.target"/>.</summary>
    ApplyModifier,
    /// <summary>Moves the hero forward <see cref="SkillEvent.distance"/> over <see cref="SkillEvent.duration"/>.</summary>
    Dash,
    /// <summary>Absorb shield of <see cref="SkillEvent.value"/> for <see cref="SkillEvent.duration"/> on the target (L1-06).</summary>
    AbsorbShield,
    Projectile,
}

public enum SkillTarget : byte
{
    Self,
    OwnSquad,
    AlliesInRadius,
}

/// <summary>
/// One phase of a skill (skill-lifecycle.md §1): a negative duration never expires (the wait of a charge, §4); only an
/// interruptible phase lets another skill in (§3.1); the final phase is the release one, always the last.
/// </summary>
[Serializable]
public struct SkillPhase
{
    public float duration;
    public bool interruptible;
    public bool final;
    public bool charge;
}

/// <summary>Something a skill does at <see cref="time"/> seconds into phase <see cref="phase"/>.</summary>
[Serializable]
public struct SkillEvent
{
    public int phase;
    public float time;
    public SkillEventType type;
    public float radius;
    /// <summary>Full cone angle in degrees; 0 or 360 = circle.</summary>
    public float angle;
    /// <summary>Damage multiplier (area damage), modifier value or shield amount.</summary>
    public float value;
    public float duration;
    public float distance;
    public StatId stat;
    public ModifierOp op;
    public SkillTarget target;
}

/// <summary>A skill as data, built from its <see cref="HeroAbility"/> (or the hero's weapon timings for the basic attack).</summary>
public struct SkillBlob
{
    public int id;
    public float staminaCost;
    public float cooldown;
    /// <summary>Cooldown group (L2-5); 0 = no cooldown.</summary>
    public int cooldownGroup;
    public SkillRefresh refresh;
    /// <summary>Skill id that may follow this one in a chain (combo), or 0.</summary>
    public int nextSkillId;
    /// <summary>Seconds after this skill ends during which the next of the chain is accepted.</summary>
    public float chainWindow;
    public BlobArray<SkillPhase> phases;
    public BlobArray<SkillEvent> events;
}

/// <summary>The skills a hero can launch, one or more per slot (the basic attack chain has one per hit).</summary>
public struct HeroSkillElement : IBufferElementData
{
    public SkillSlot slot;
    public int skillId;
    public BlobAssetReference<SkillBlob> skill;
}
