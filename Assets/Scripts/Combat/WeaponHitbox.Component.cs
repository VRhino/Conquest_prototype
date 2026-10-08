using Unity.Entities;

/// <summary>
/// IEnableableComponent tag that gates hitbox processing.
/// Added to the unit entity at spawn; enabled ONLY during the strike window
/// by attack systems. MeleeContactQueue reads this tag as an early gate;
/// MeleeContactResolutionSystem validates it again before creating damage.
/// </summary>
public struct WeaponHitboxActiveTag : IComponentData, IEnableableComponent { }
