using Unity.Entities;

/// <summary>
/// Enableable tag set by SquadAnchorSystem each frame.
/// Enabled when the squad's formation anchor moved this frame (hero is moving).
/// Disabled when the anchor is stationary (hero stopped).
/// </summary>
public struct SquadAnchorMovingTag : IComponentData, IEnableableComponent { }
