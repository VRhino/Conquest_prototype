using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Rotation priority levels. Higher value wins.
/// </summary>
public enum RotationSource
{
    NavMesh   = 0,  // NavMesh auto-rotation (default, no override)
    Formation = 5,  // Formation discipline — Formed units face squad direction
    Combat    = 10  // Combat engagement — unit faces its target
}

/// <summary>
/// Written each frame by systems that want to control a unit's rotation.
/// UnitRotationResolutionSystem reads the highest-priority intent and applies
/// it to agent.transform.rotation — the single writer of that field.
/// Reset to priority=0 after consumption each frame.
/// </summary>
public struct UnitRotationIntentComponent : IComponentData
{
    /// <summary>Desired final rotation for this frame.</summary>
    public quaternion targetRotation;

    /// <summary>Priority — higher wins. See RotationSource enum.</summary>
    public int priority;

    /// <summary>Which system wrote this intent.</summary>
    public RotationSource source;
}
