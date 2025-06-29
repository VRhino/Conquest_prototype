using Unity.Entities;

/// <summary>
/// Associates an individual unit with its owning squad and hero.
/// </summary>
public struct UnitOwnerComponent : IComponentData
{
    /// <summary>Squad entity that this unit belongs to.</summary>
    public Entity squad;

    /// <summary>Hero entity commanding the squad.</summary>
    public Entity hero;
}
