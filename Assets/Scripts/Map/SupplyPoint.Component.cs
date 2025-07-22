using Unity.Entities;

/// <summary>
/// Data for zones of type <see cref="ZoneType.Supply"/>.
/// Tracks capture state and ownership.
/// </summary>
public struct SupplyPointComponent : IComponentData
{
    /// <summary>Current capture progress percentage.</summary>
    public float captureProgress;

    /// <summary>Amount of progress gained per second.</summary>
    public float captureSpeed;

    /// <summary>Team that currently owns the supply point.</summary>
    public int currentTeam;

    /// <summary>True when heroes from both teams are present.</summary>
    public bool isContested;
}
