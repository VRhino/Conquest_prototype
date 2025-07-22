using Unity.Entities;

public enum HeroState
{
    Idle,
    Moving
}

public struct HeroStateComponent : IComponentData
{
    public HeroState State;
}
