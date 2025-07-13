using Unity.Entities;

/// <summary>
/// Component that tracks the current AI state of a squad.
/// </summary>
public struct SquadAIComponent : IComponentData
{
    /// <summary>Current AI state.</summary>
    public TacticalIntent  tacticalIntent;

    /// <summary>Current target entity if any.</summary>
    public Entity targetEntity;

    /// <summary>True if the squad is actively engaged in combat.</summary>
    public bool isInCombat;
}
