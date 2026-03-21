using Unity.Entities;

/// <summary>
/// IEnableableComponent tag that gates hitbox processing.
/// Added to the unit entity at spawn; enabled ONLY during the strike window
/// by UnitAttackSystem. WeaponHitboxBehaviour reads this tag as a gate.
/// </summary>
public struct WeaponHitboxActiveTag : IComponentData, IEnableableComponent { }
