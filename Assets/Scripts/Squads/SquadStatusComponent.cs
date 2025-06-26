using Unity.Entities;

/// <summary>
/// Tracks the overall state of the currently selected squad.
/// </summary>
public struct SquadStatusComponent : IComponentData
{
    /// <summary>Number of units still alive.</summary>
    public int aliveUnits;

    /// <summary>Total number of units in the squad.</summary>
    public int totalUnits;

    /// <summary>Identifier of the formation currently in use.</summary>
    public int activeFormationId;

    /// <summary>Cooldown remaining on the last issued formation change.</summary>
    public float formationCooldown;
}
