using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Stores input values for the hero. Other systems can read these values
/// to drive movement, abilities and combat.
/// </summary>
public struct HeroInputComponent : IComponentData
{
    /// <summary>Direction on the XZ plane from WASD keys.</summary>
    public float2 moveInput;

    /// <summary>True while left shift is held.</summary>
    public bool isSprinting;

    /// <summary>True while left mouse button is held.</summary>
    public bool isAttacking;

    /// <summary>True while Q key is held.</summary>
    public bool useSkill1;

    /// <summary>True while E key is held.</summary>
    public bool useSkill2;

    /// <summary>True while R key is held.</summary>
    public bool useUltimate;
}
