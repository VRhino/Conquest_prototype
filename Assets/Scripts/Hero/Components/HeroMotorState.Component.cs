using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Confirmed result of the local hero's CharacterController step.
/// Gameplay reads this state; it must not be used to command movement.
/// </summary>
public struct HeroMotorStateComponent : IComponentData
{
    public float3 velocity;
    public bool isGrounded;
    public bool hitSides;
    public bool hitCeiling;
}
