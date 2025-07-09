using Unity.Entities;

/// <summary>
/// Temporary flag component updated each frame with the interaction input.
/// Systems can read this to trigger contextual actions.
/// </summary>
public struct PlayerInteractionComponent : IComponentData
{
    /// <summary>True if the player pressed the interaction key this frame.</summary>
    public bool interactPressed;
}
