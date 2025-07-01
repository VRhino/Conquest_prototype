using Unity.Entities;

public enum UnitFormationState
{
    Formed,
    Moving
}

public struct UnitFormationStateComponent : IComponentData
{
    public UnitFormationState State;
    public float Delay;      // Only used for Formed→Moving transition
    public float Timer;      // Only used for Formed→Moving transition
    public bool Waiting;     // Only used for Formed→Moving transition
}
