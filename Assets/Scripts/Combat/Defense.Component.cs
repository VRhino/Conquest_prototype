using Unity.Entities;

/// <summary>
/// Defense values against each type of damage. Written by <see cref="StatResolveSystem"/> from the modifier stack.
/// </summary>
public struct DefenseComponent : IComponentData
{
    /// <summary>Resistance against blunt damage.</summary>
    public float bluntDefense;

    /// <summary>Resistance against slashing damage.</summary>
    public float slashDefense;

    /// <summary>Resistance against piercing damage.</summary>
    public float pierceDefense;

    /// <summary>
    /// Percentage relief of each type (L1-19): the fraction taken off a type's damage after the flat defense and its
    /// floor; 0 = none, capped by <see cref="CombatTuning.maxRelief"/>.
    /// </summary>
    public float bluntRelief;
    public float slashRelief;
    public float pierceRelief;
}
