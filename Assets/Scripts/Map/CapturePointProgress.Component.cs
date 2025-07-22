using Unity.Entities;

/// <summary>
/// Tracks capture progress for a capture zone.
/// </summary>
public struct CapturePointProgressComponent : IComponentData
{
    /// <summary>Current capture percentage (0-100).</summary>
    public float captureProgress;

    /// <summary>Speed of capture per second.</summary>
    public float captureSpeed;

    /// <summary>Team currently attempting to capture.</summary>
    public int capturingTeam;

    /// <summary>True if defenders are present, pausing capture.</summary>
    public bool isContested;
}
