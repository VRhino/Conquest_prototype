using Unity.Entities;

/// <summary>
/// Tracks the equipment integrity of a unit. Values are persisted between battles.
/// </summary>
public struct UnitEquipmentComponent : IComponentData
{
    /// <summary>Percentage of armor remaining (0-100).</summary>
    public float armorPercent;

    /// <summary>True when the unit suffers penalties due to low armor.</summary>
    public bool hasDebuff;

    /// <summary>False when the unit cannot be deployed.</summary>
    public bool isDeployable;
}
