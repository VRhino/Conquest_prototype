using Unity.Entities;

/// <summary>
/// Holds references to a squad skill and its execution data.
/// </summary>
public struct SquadSkillComponent : IComponentData
{
    /// <summary>Entity containing parameters or effects for the skill.</summary>
    public Entity skillData;

    /// <summary>Stamina or resource cost required to use the skill.</summary>
    public float staminaCost;

    /// <summary>Entity with the <see cref="CooldownComponent"/> controlling this skill.</summary>
    public Entity cooldownEntity;
}
