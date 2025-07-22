using Unity.Entities;

/// <summary>
/// Identifies which squad instance a hero should spawn with.
/// </summary>
public struct HeroSquadSelectionComponent : IComponentData
{
    /// <summary>Entity holding static data for the selected squad.</summary>
    public Entity squadDataEntity;

    /// <summary>Unique identifier for the squad instance.</summary>
    public int instanceId;
}
