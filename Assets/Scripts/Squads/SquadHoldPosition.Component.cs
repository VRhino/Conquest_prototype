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
    /// The hero's facing rotation when Hold Position was ordered.
    /// Used to rotate the formation so units face the same direction.
    /// </summary>
    public quaternion holdRotation;

    /// <summary>
    /// The formation type that was active when Hold Position was ordered.
    /// This prevents formation changes from affecting the held position.
    /// </summary>
    public FormationType originalFormation;

    /// <summary>
    /// How far the formation has advanced from <see cref="holdCenter"/> with the Advance ability. Only
    /// <see cref="SquadAdvanceSystem"/> moves it; <see cref="SquadOrderSystem"/> starts it at zero with every new Hold
    /// Position order.
    /// </summary>
    public float3 advanceOffset;

    /// <summary>Where the squad holds now: the ordered centre plus what it has advanced.</summary>
    public float3 Centre => holdCenter + advanceOffset;
}
