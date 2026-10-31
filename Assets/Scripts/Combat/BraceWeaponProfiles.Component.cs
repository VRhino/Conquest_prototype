using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Tracks whether a squad is in normal or brace combat mode.
/// Set by BraceWeaponActivationSystem when the squad enters HoldPosition.
/// </summary>
public enum SquadCombatMode : byte
{
    Normal = 0,
    Brace  = 1
}

/// <summary>
/// Current combat mode of a squad entity.
/// </summary>
public struct SquadCombatModeComponent : IComponentData
{
    public SquadCombatMode mode;
    public float3 forward;
    public float halfAngleRadians;

    public static bool TryGetBrace(EntityManager em, Entity unit, out SquadCombatModeComponent brace)
    {
        brace = default;
        if (!em.HasComponent<UnitOwnerComponent>(unit)) return false;
        var squad = em.GetComponentData<UnitOwnerComponent>(unit).squad;
        if (!em.HasComponent<SquadCombatModeComponent>(squad)) return false;
        brace = em.GetComponentData<SquadCombatModeComponent>(squad);
        return brace.mode == SquadCombatMode.Brace;
    }

    public static bool InsideArc(float3 forward, float3 direction, float halfAngle)
    {
        float2 heading = math.normalizesafe(forward.xz, new float2(0f, 1f));
        float2 toward = math.normalizesafe(direction.xz, heading);
        return math.dot(heading, toward) >= math.cos(halfAngle);
    }

    public static bool AllowsTarget(EntityManager em, Entity unit, Entity target)
    {
        if (!TryGetBrace(em, unit, out var brace)) return true;
        if (!em.HasComponent<LocalTransform>(target) || !em.HasComponent<LocalTransform>(unit)) return false;
        var delta = em.GetComponentData<LocalTransform>(target).Position - em.GetComponentData<LocalTransform>(unit).Position;
        float range = em.HasComponent<UnitRangedStatsComponent>(unit)
            ? em.GetComponentData<UnitRangedStatsComponent>(unit).range
            : em.HasComponent<UnitWeaponComponent>(unit) ? em.GetComponentData<UnitWeaponComponent>(unit).attackRange : 0f;
        return math.lengthsq(delta.xz) <= range * range && InsideArc(brace.forward, delta, brace.halfAngleRadians);
    }
}

/// <summary>
/// Per-row weapon parameter override applied to UnitWeaponComponent during Brace mode.
/// Stored as a DynamicBuffer on the squad entity.
/// Row 0 = front rank. BraceWeaponSystem applies these overrides each frame.
/// </summary>
public struct BraceRowProfile : IBufferElementData
{
    /// <summary>Formation row index this profile applies to (0 = front rank).</summary>
    public int row;

    /// <summary>Distance from unit origin to far edge of damage box (= attackRange).</summary>
    public float attackRange;

    /// <summary>Seconds from animation start when hitbox activates.</summary>
    public float strikeWindowStart;

    /// <summary>Duration the hitbox stays active.</summary>
    public float strikeWindowDuration;

    /// <summary>Total duration of the attack animation.</summary>
    public float attackAnimationDuration;
}
