using Unity.Entities;

/// <summary>
/// Represents whether the hero is currently grounded.
/// Other systems should update this flag based on collisions.
/// </summary>
public struct HeroGroundState : IComponentData
{
    public bool isGrounded;
}
