using Unity.Entities;

/// <summary>
/// Pulse tag: enabled for exactly one frame when a unit transitions from Waiting/Formed → Moving.
/// UnitFormationStateSystem disables it at the start of each unit's update, then re-enables it
/// only when the transition fires. Use WithAll&lt;UnitStartedMovingTag&gt; to react.
/// </summary>
public struct UnitStartedMovingTag : IComponentData, IEnableableComponent { }

/// <summary>
/// Pulse tag: enabled for exactly one frame when a unit transitions from Moving → Formed.
/// UnitFormationStateSystem disables it at the start of each unit's update, then re-enables it
/// only when the transition fires. Use WithAll&lt;UnitArrivedAtSlotTag&gt; to react.
/// </summary>
public struct UnitArrivedAtSlotTag : IComponentData, IEnableableComponent { }
