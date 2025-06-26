using Unity.Entities;

/// <summary>
/// Tracks progression data for a squad including level and unlocked content.
/// </summary>
public struct SquadProgressComponent : IComponentData
{
    /// <summary>Current squad level.</summary>
    public int level;

    /// <summary>Experience accumulated towards the next level.</summary>
    public float currentXP;

    /// <summary>XP required to reach the next level.</summary>
    public float xpToNextLevel;
}

/// <summary>
/// Buffer element holding references to unlocked squad abilities.
/// </summary>
public struct UnlockedAbilityElement : IBufferElementData
{
    public Entity Value;
}

/// <summary>
/// Buffer element storing formations available to the squad.
/// </summary>
public struct UnlockedFormationElement : IBufferElementData
{
    public FormationType Value;
}

