using Unity.Entities;

/// <summary>
/// Buffer element containing enemy entities detected by a single unit.
/// </summary>
public struct UnitDetectedEnemy : IBufferElementData
{
    /// <summary>Reference to the detected enemy.</summary>
    public Entity Value;
}
