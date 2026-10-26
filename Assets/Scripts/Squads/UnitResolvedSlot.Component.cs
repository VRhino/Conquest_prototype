using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Formation slot a unit actually moves to: the template slot (<see cref="UnitTargetPositionComponent"/>) checked
/// against the NavMesh, or the nearest free cell when the template is blocked or taken (C13). Single writer:
/// <see cref="FormationSlotResolveSystem"/>. Movement, arrival and avoidance read this instead of the template.
/// </summary>
public struct UnitResolvedSlotComponent : IComponentData
{
    /// <summary>Where the unit should stand, m.</summary>
    public float3 position;
    /// <summary>Template position at the last validation; the slot is validated again once the template moves away.</summary>
    public float3 validatedTemplate;
    /// <summary>Resolved minus template at the last validation; carried while the template moves less than the threshold.</summary>
    public float3 correction;
    /// <summary>Whole cells moved from the template at the last validation (formation frame).</summary>
    public int2 shift;
    public FormationSlotResolveStatus status;

    /// <summary>
    /// True when the squad check found the slot on the NavMesh: navigation projects it only within its own cell, never
    /// with the wide fallback that piled slots on a wall or put them behind it.
    /// </summary>
    public bool IsOnNavMesh => status == FormationSlotResolveStatus.Template || status == FormationSlotResolveStatus.Relocated;
}
