using Unity.Entities;

/// <summary>
/// Tag that marks an entity as detectable by EnemyDetectionSystem.
/// Add to heroes, bosses, towers, or any future entity that squads should target.
/// </summary>
public struct DetectableEntityTag : IComponentData { }
