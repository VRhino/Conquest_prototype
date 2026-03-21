using Unity.Entities;

/// <summary>
/// Enableable tag present on all unit entities.
/// Enabled while the unit has an active combat target (UnitCombatComponent.target != Null).
/// Used by UnitFollowFormationSystem to skip formation-orientation overrides
/// for units that are actively engaged in combat.
/// </summary>
public struct IsEngagingTag : IComponentData, IEnableableComponent { }
