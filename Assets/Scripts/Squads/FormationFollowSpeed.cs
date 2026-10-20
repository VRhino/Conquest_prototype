using Unity.Mathematics;

/// <summary>
/// Slot-following speed with recovery (Movimiento_Formaciones.md L-3 with decision D4): the unit moves at the
/// slot's speed plus a proportional catch-up term, never faster than its current gear, and switches to Run
/// when it falls far behind (with hysteresis so the gear does not flicker).
/// </summary>
public static class FormationFollowSpeed
{
    /// <summary>
    /// Below this slot speed the slot is treated as still: the unit arrives at its gear speed and the
    /// agent's own braking, as before C9, instead of crawling in on the recovery term.
    /// </summary>
    public const float StillSlotSpeed = 0.5f;

    public struct Settings
    {
        public float arrivalRadius;     // No catch-up inside this distance (r_in).
        public float recoveryTime;      // Seconds to close the gap (T_rec).
        public float minSpeedRatio;     // Floor as a fraction of the base speed.
        public float runEnterDistance;  // Switch to Run beyond this distance to the slot.
        public float runExitDistance;   // Back to Walk below this distance.
        public float runSpeedMultiplier;

        public static Settings From(in MovementTuningComponent t) => new Settings
        {
            arrivalRadius = t.arrivalRadiusIn,
            recoveryTime = t.followRecoveryTime,
            minSpeedRatio = t.followMinSpeedRatio,
            runEnterDistance = t.runEnterDistance,
            runExitDistance = t.runExitDistance,
            runSpeedMultiplier = t.runSpeedMultiplier,
        };
    }

    /// <summary>Gear for this frame given the previous one; forced Run (hurry, combat) wins.</summary>
    public static MovementGear Gear(MovementGear previous, float distanceToSlot, bool forceRun, in Settings s)
    {
        if (forceRun || distanceToSlot > s.runEnterDistance) return MovementGear.Run;
        if (distanceToSlot < s.runExitDistance) return MovementGear.Walk;
        return previous;
    }

    /// <summary>
    /// Speed cap of this frame: rises towards <paramref name="target"/> by at most <paramref name="maxDelta"/>
    /// (acceleration × dt) and falls by at most <paramref name="maxBrakeDelta"/>. A sudden lower cap made the agent
    /// cut its velocity in one frame.
    /// </summary>
    public static float Approach(float current, float target, float maxDelta, float maxBrakeDelta)
    {
        if (maxDelta <= 0f) return target;
        return current + math.clamp(target - current, -math.max(maxDelta, maxBrakeDelta), maxDelta);
    }

    /// <summary>
    /// Highest speed from which the unit can still stop within <paramref name="distance"/> braking at
    /// <paramref name="deceleration"/>: √(2·a·d). Capping the speed with it makes a unit heading for a still
    /// destination brake on the way in; without it the agent stopped in one frame on arrival (M12/M13).
    /// <paramref name="deceleration"/> ≤ 0 means no cap.
    /// </summary>
    public static float ArrivalSpeed(float distance, float deceleration)
    {
        if (deceleration <= 0f) return float.PositiveInfinity;
        return math.sqrt(2f * deceleration * math.max(0f, distance));
    }

    /// <summary>Whether the destination stays put, so the unit should brake into it: a slot slower than
    /// <see cref="StillSlotSpeed"/>, or any non-formation destination (melee stop point).</summary>
    public static bool IsStillDestination(bool commandIsFormation, float slotSpeed)
        => !commandIsFormation || slotSpeed < StillSlotSpeed;

    /// <summary>Top speed of a gear.</summary>
    public static float MaxSpeed(float baseSpeed, MovementGear gear, in Settings s)
        => baseSpeed * (gear == MovementGear.Run ? s.runSpeedMultiplier : 1f);

    /// <summary>
    /// Speed command: v_ff + max(0, (d − r_in)/T), clamped to [minRatio·base, gear speed]; a still slot is
    /// approached at the gear speed.
    /// </summary>
    public static float Speed(float baseSpeed, float slotSpeed, float distanceToSlot, MovementGear gear, in Settings s)
    {
        float max = MaxSpeed(baseSpeed, gear, s);
        if (slotSpeed < StillSlotSpeed) return max;
        float min = math.min(max, baseSpeed * s.minSpeedRatio);
        float recovery = math.max(0f, distanceToSlot - s.arrivalRadius) / math.max(1e-3f, s.recoveryTime);
        return math.clamp(slotSpeed + recovery, min, max);
    }
}
