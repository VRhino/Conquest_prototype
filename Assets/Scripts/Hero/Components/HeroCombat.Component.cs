using Unity.Entities;

/// <summary>
/// Stores combat related data for the hero such as the equipped weapon
/// and attack cooldown state.
/// </summary>
public struct HeroCombatComponent : IComponentData
{
    /// <summary>Entity representing the currently active weapon.</summary>
    public Entity activeWeapon;

    /// <summary>Cooldown time remaining before the next attack.</summary>
    public float attackCooldown;

    /// <summary>Set when the hero is in the middle of an attack animation.</summary>
    public bool isAttacking;
}
