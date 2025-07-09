using Unity.Entities;

/// <summary>
/// Stores persistent progression data for the player's hero.
/// </summary>
public struct HeroProgressComponent : IComponentData
{
    /// <summary>Current hero level.</summary>
    public int level;

    /// <summary>Experience points accumulated towards the next level.</summary>
    public int currentXP;

    /// <summary>XP required to reach the next level.</summary>
    public int xpToNextLevel;

    /// <summary>Perk points available to spend.</summary>
    public int perkPoints;
}
