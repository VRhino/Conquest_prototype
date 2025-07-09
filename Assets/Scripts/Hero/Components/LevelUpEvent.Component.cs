using Unity.Entities;

/// <summary>
/// Event emitted when the hero gains a level.
/// </summary>
public struct LevelUpEvent : IComponentData
{
    /// <summary>New level obtained.</summary>
    public int newLevel;
}
