using Unity.Entities;

/// <summary>
/// Describes the current state of the camera.
/// </summary>
public enum CameraState
{
    Normal,
    Tactical,
    Spectator
}

/// <summary>
/// Component that stores the camera state.
/// </summary>
public struct CameraStateComponent : IComponentData
{
    public CameraState state;
}
