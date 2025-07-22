using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Holds the preferred formation for a squad and the dynamic pattern
/// used by formation systems. The pattern can be modified at runtime
/// by other systems.
/// </summary>
public struct FormationComponent : IComponentData
{
    /// <summary>Formation selected by the player.</summary>
    public FormationType currentFormation;
}

/// <summary>
/// Buffer element storing offset positions for a formation pattern.
/// </summary>
public struct FormationPatternElement : IBufferElementData
{
    public float3 Value;
}
