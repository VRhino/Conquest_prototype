using Unity.Entities;

public enum UnitFormationState
{
    Formed,   // Unit is in its assigned cell and hero is within grid radius
    Waiting,  // Hero leaves grid radius, unit waits a random delay before moving
    Moving    // Unit is moving to its slot; returns to Formed when it arrives and hero is in range
}

public struct UnitFormationStateComponent : IComponentData
{
    public UnitFormationState State;
    public float DelayTimer;     // Timer for Waiting state random delay
    public float DelayDuration;  // Duration of the delay (0.5-1.5s)
}
