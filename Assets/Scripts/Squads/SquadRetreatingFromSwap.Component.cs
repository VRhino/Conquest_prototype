using Unity.Entities;

/// <summary>
/// Tag placed on a squad entity retreating due to a swap (not hero death).
/// Carries metadata needed to persist the squad's alive-unit count back into
/// the hero's <see cref="InactiveSquadElement"/> buffer.
/// </summary>
public struct SquadRetreatingFromSwapTag : IComponentData
{
    /// <summary>Squad instance ID for updating the correct InactiveSquadElement.</summary>
    public int squadId;

    /// <summary>Reference to the hero entity that owns the InactiveSquadElement buffer.</summary>
    public Entity heroEntity;
}
