using Unity.Entities;

/// <summary>
/// Walking speed of a hero resolved from the modifier stack (<see cref="StatId.MoveSpeed"/>), written by
/// <see cref="StatResolveSystem"/>; 0 while immobilised (a shield-break stun). Only heroes on the stack have it: movement
/// falls back to <see cref="HeroStatsComponent.baseSpeed"/>, which stays the reference for gait and sprint detection.
/// Squad units use <see cref="UnitStatsComponent.speed"/> instead.
/// </summary>
public struct MoveSpeedComponent : IComponentData
{
    public float value;
}
