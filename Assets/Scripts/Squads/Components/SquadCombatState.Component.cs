using Unity.Entities;

/// <summary>
/// Tracks the combat engagement state of a squad.
/// Single source of truth for combat state — owned exclusively by SquadAISystem.
/// </summary>
public struct SquadCombatStateComponent : IComponentData
{
    /// <summary>True if the squad is actively engaged in combat.</summary>
    public bool isInCombat;

    /// <summary>The primary enemy entity this squad is engaging, if any.</summary>
    public Entity engagedTarget;
}
