using Unity.Entities;

/// <summary>
/// Component storing general squad configuration values independent of units.
/// </summary>
public struct SquadStatsComponent : IComponentData
{
    public SquadType squadType;
    public BehaviorProfile behaviorProfile;
}
