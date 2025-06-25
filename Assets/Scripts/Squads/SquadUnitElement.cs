using Unity.Entities;

/// <summary>
/// Buffer element used to store the entities that belong to a squad.
/// The first element is considered the leader.
/// </summary>
public struct SquadUnitElement : IBufferElementData
{
    /// <summary>Entity representing the squad member.</summary>
    public Entity Value;
}
