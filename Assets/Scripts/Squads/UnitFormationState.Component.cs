using Unity.Entities;

public struct UnitFormationStateComponent : IComponentData
{
    public UnitFormationState State;
    public float DelayTimer;     // Timer for Waiting state random delay
    public float DelayDuration;  // Duration of the delay (0.5-1.5s)
}
