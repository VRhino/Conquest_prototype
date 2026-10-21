using Unity.Entities;

/// <summary>
/// A pending launch request from the player's input or the AI (L2-1). <see cref="SkillExecutionSystem"/> evaluates it
/// with <see cref="SkillGate"/> and disables it.
/// </summary>
public struct SkillRequestComponent : IComponentData, IEnableableComponent
{
    public SkillSlot slot;
    public double requestedAt;
    /// <summary>Slots whose key is held this frame (bit = 1 &lt;&lt; slot), written every frame even with no request: releasing
    /// the key of a charging skill moves it to its release phase (L2-6).</summary>
    public byte heldSlots;

    public static byte Bit(SkillSlot slot) => (byte)(1 << (int)slot);
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

/// <summary>
/// The skill a <see cref="SkillRefresh.Yield"/> skill set aside (L2-4, skill-lifecycle.md §3.2): only one. It keeps its
/// clock running while parked and becomes the current one again when the yield skill ends, if it has not ended itself.
/// </summary>
public struct ParkedSkillComponent : IComponentData
{
    public bool active;
    public SkillInstanceComponent instance;
}

/// <summary>
/// Asks <see cref="SkillExecutionSystem"/> to end the hero's skills, current and parked (external endings of the
/// reference: here a squad swap). Death, the end of the battle and control end them without asking.
/// </summary>
public struct EndSkillsRequestComponent : IComponentData, IEnableableComponent { }
