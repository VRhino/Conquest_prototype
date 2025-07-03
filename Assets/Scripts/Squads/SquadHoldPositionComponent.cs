using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Stores the fixed center position for a squad when in Hold Position mode.
/// This allows all units to maintain their formation relative to a fixed point
/// instead of following the moving hero.
/// </summary>
public struct SquadHoldPositionComponent : IComponentData
{
    /// <summary>
    /// The fixed center position where the squad should maintain formation.
    /// This is set when the Hold Position order is given.
    /// </summary>
    public float3 holdCenter;
    
    /// <summary>
    /// The formation type that was active when Hold Position was ordered.
    /// This prevents formation changes from affecting the held position.
    /// </summary>
    public FormationType originalFormation;
}
