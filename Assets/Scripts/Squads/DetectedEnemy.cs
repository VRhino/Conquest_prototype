using Unity.Entities;

/// <summary>
/// Buffer element containing detected enemy entities within range of a squad.
/// </summary>
public struct DetectedEnemy : IBufferElementData
{
    /// <summary>Enemy entity reference.</summary>
    public Entity Value;
}
