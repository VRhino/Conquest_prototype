using Unity.Entities;

/// <summary>
/// Runtime component representing a perk definition.
/// Currently only stores the unique identifier used by other systems.
/// </summary>
public struct PerkDataComponent : IComponentData
{
    public int perkID;
}
