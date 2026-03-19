using Unity.Entities;

/// <summary>
/// Identifies the owner unit of a weapon hitbox entity.
/// Added to the WeaponHitboxEntity created at spawn.
/// </summary>
public struct WeaponHitboxOwner : IComponentData
{
    /// <summary>The unit entity that owns this hitbox.</summary>
    public Entity ownerUnit;
}

/// <summary>
/// References the weapon hitbox entity belonging to a unit.
/// Added to the main UnitEntity at spawn.
/// </summary>
public struct WeaponHitboxRef : IComponentData
{
    /// <summary>The weapon hitbox entity belonging to this unit.</summary>
    public Entity hitboxEntity;
}

/// <summary>
/// IEnableableComponent tag that gates hitbox processing.
/// Added to WeaponHitboxEntity; enabled ONLY during the strike window.
/// HitboxCollisionSystem skips entities with this tag disabled.
/// </summary>
public struct WeaponHitboxActiveTag : IComponentData, IEnableableComponent { }

/// <summary>
/// Physics layer bitmasks for hitbox/hurtbox collision filtering.
/// Configure matching layers 6 (Hurtbox) and 7 (Hitbox) in
/// Unity's Physics Category Names settings.
/// </summary>
public static class PhysicsLayers
{
    /// <summary>Layer 6 — hurtbox capsules on unit entities (receive damage).</summary>
    public const uint HurtboxMask = 1u << 6;

    /// <summary>Layer 7 — weapon hitbox boxes on WeaponHitboxEntities (deal damage).</summary>
    public const uint HitboxMask = 1u << 7;
}
