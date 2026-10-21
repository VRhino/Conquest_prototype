using Unity.Entities;

/// <summary>
/// A pending launch request from the player's input or the AI (L2-1). <see cref="SkillExecutionSystem"/> evaluates it
/// with <see cref="SkillGate"/> and disables it.
/// </summary>
public struct SkillRequestComponent : IComponentData, IEnableableComponent
{
    public SkillSlot slot;
    public double requestedAt;
}

/// <summary>
/// The skill a hero is running (L2-2): which entry of its <see cref="HeroSkillElement"/> buffer, the phase and the time
/// in it. One per hero; the parked one (L2-4) has its own component.
/// </summary>
public struct SkillInstanceComponent : IComponentData
{
    public bool active;
    public int skillIndex;
    public int phase;
    public float phaseTime;
    /// <summary>Counter of launches, so events and hits can name the instance.</summary>
    public uint instanceId;
    /// <summary>The weapon hitbox is open (between the skill's HitboxOpen and HitboxClose events).</summary>
    public bool hitboxOpen;
    /// <summary>Id of the last skill that ended, and when, for chains (L2-3).</summary>
    public int lastSkillId;
    public double lastEndedAt;
}
