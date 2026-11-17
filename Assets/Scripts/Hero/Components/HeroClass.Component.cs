using Unity.Entities;

/// <summary>
/// The hero's class: its weapon family (animations, horse and look) and the id of its class asset, the engine's
/// (<see cref="HeroClassManager"/>; empty: the family's name).
/// </summary>
public struct HeroClassComponent : IComponentData
{
    public HeroClass heroClass;
    public Unity.Collections.FixedString64Bytes classId;
}
