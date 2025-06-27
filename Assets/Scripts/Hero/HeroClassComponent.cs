using Unity.Entities;

/// <summary>
/// Simple component storing the hero class type.
/// </summary>
public struct HeroClassComponent : IComponentData
{
    public HeroClass heroClass;
}
