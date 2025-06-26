using Unity.Entities;

/// <summary>
/// Event emitted whenever a squad gains a new level.
/// </summary>
public struct SquadLevelUpEvent : IComponentData
{
    /// <summary>Squad entity that leveled up.</summary>
    public Entity squad;
}
