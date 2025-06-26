using Unity.Entities;

/// <summary>
/// Event created when a squad swap is successfully performed.
/// Systems listening to this can update HUD elements or
/// synchronize the change in multiplayer scenarios.
/// </summary>
public struct SquadChangeEvent : IComponentData
{
    /// <summary>ID of the new active squad.</summary>
    public int newSquadId;
}
