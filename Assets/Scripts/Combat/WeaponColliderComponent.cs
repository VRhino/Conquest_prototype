using Unity.Entities;

/// <summary>
/// Component placed on a weapon collider entity. It is enabled only during
/// the impact frames of an attack animation so that collisions can be
/// detected and damage events generated.
/// </summary>
public struct WeaponColliderComponent : IComponentData
{
    /// <summary>Owner entity that initiated the attack.</summary>
    public Entity owner;

    /// <summary>True while the collider should detect hits.</summary>
    public bool isActive;
}
