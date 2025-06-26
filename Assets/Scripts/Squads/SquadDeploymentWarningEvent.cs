using Unity.Entities;

/// <summary>
/// Event generated when a squad has deployment issues.
/// Used by the HUD to display warnings.
/// </summary>
public struct SquadDeploymentWarningEvent : IComponentData
{
    /// <summary>Squad entity that triggered the warning.</summary>
    public Entity squad;

    /// <summary>True if the squad can be deployed.</summary>
    public bool isDeployable;

    /// <summary>True if the squad suffers debuffs due to low armor.</summary>
    public bool hasDebuff;
}
