using Unity.Entities;

/// <summary>
/// Defense values against each type of damage.
/// </summary>
public struct DefenseComponent : IComponentData
{
    /// <summary>Resistance against blunt damage.</summary>
    public float bluntDefense;

    /// <summary>Resistance against slashing damage.</summary>
    public float slashDefense;

    /// <summary>Resistance against piercing damage.</summary>
    public float pierceDefense;
}
