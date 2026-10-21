using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// An effect event of a running skill (area damage, modifier, absorb shield, dash), queued by
/// <see cref="SkillExecutionSystem"/> with the caster's pose and applied once by <see cref="SkillEffectSystem"/>.
/// </summary>
public struct SkillEffectRequestComponent : IComponentData
{
    public Entity caster;
    public int skillId;
    public SkillEvent effect;
    public float3 origin;
    /// <summary>Caster's facing on the ground plane (normalised).</summary>
    public float3 forward;
    public Team team;
}

/// <summary>A dash in progress (<see cref="SkillEventType.Dash"/>): the hero moves along it instead of following input or path.</summary>
public struct HeroDashComponent : IComponentData
{
    public float3 direction;
    public float speed;
    public double endTime;

    public bool Active(double now) => now < endTime;
}
