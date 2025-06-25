using Unity.Entities;

/// <summary>
/// Holds adjustable settings for the third person camera.
/// </summary>
public struct CameraSettingsComponent : IComponentData
{
    /// <summary>Speed at which the camera zooms.</summary>
    public float zoomSpeed;

    /// <summary>Sensitivity of the horizontal rotation.</summary>
    public float rotationSensitivity;

    /// <summary>Minimum allowed zoom level.</summary>
    public float minZoom;

    /// <summary>Maximum allowed zoom level.</summary>
    public float maxZoom;
}
