using Unity.Entities;

/// <summary>
/// Cooldown after a successful squad swap (H7). It lives in the hero's <see cref="CooldownGroupElement"/> buffer like the
/// skills, so it needs no per-frame system and the HUD reads it the same way.
/// </summary>
public static class SquadSwapCooldown
{
    /// <summary>Group reserved for the swap; skill groups are small (one per slot or authored).</summary>
    public const int Group = 1000;
    /// <summary>Seconds before another swap is allowed. Not scaled by <see cref="CooldownScaleComponent"/>.</summary>
    public const float Duration = 10f;

    public static float Remaining(EntityManager em, Entity hero, double now)
        => em.HasBuffer<CooldownGroupElement>(hero)
            ? CooldownUtility.Remaining(em.GetBuffer<CooldownGroupElement>(hero, true), Group, now)
            : 0f;

    public static bool IsActive(EntityManager em, Entity hero, double now) => Remaining(em, hero, now) > 0f;
}
