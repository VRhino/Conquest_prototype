using Unity.Entities;

/// <summary>
/// Tag that identifies a hero entity controlled by the AI pipeline.
/// Added to every remote (non-local-player) hero at spawn.
/// Never present on the local player hero.
/// </summary>
public struct HeroAITag : IComponentData { }
