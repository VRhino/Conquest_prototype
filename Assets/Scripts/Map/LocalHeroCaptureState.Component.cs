using Unity.Entities;

/// <summary>
/// Singleton component that tracks whether the local player's hero is currently
/// inside a capture or supply zone, and the current progress of that zone.
/// Written each frame by <see cref="LocalHeroCaptureTrackerSystem"/>.
/// </summary>
public struct LocalHeroCaptureStateComponent : IComponentData
{
    /// <summary>True when the local hero is inside an active zone radius.</summary>
    public bool isInZone;

    /// <summary>Capture progress of the zone (0–100).</summary>
    public float captureProgress;

    /// <summary>True when both teams are present in the zone (capture is paused).</summary>
    public bool isContested;
}
