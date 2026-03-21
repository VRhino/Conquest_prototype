using Unity.Entities;

/// <summary>
/// One-frame pulse tag enabled on a unit entity by DamageCalculationSystem
/// the moment it receives damage. Consumed (disabled) by SquadAISystem the
/// same frame to trigger squad-level retaliation (TacticalIntent.Attacking).
/// </summary>
public struct IsUnderAttackTag : IComponentData, IEnableableComponent { }
