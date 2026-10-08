using Unity.Entities;

/// <summary>
/// Holds the currently selected formation for a squad.
/// </summary>
public struct FormationComponent : IComponentData
{
    /// <summary>Formation selected by the player.</summary>
    public FormationType currentFormation;
}
