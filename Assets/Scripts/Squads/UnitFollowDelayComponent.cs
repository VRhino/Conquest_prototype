using Unity.Entities;

/// <summary>
/// Delay component for squad units to randomize their movement start after the hero leaves the squad radius.
/// </summary>
public struct UnitFollowDelayComponent : IComponentData
{
    public float delay;
    public float timer;
    public bool waiting;
    public bool triggered;
}
