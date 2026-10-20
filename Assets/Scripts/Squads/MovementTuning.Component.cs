using Unity.Entities;

/// <summary>
/// Singleton with the unit movement tuning baked from <see cref="MovementTuningConfig"/>.
/// One source of truth for arrival radii, start delays, destination updates and the movement constants
/// that used to live in system code (Docs/Design/Movimiento_Formaciones.md §3).
/// </summary>
public struct MovementTuningComponent : IComponentData
{
    // Arrival (UnitFormationStateSystem, UnitNavMeshSystem)
    public float arrivalRadiusIn;
    public float arrivalRadiusOut;
    public float arrivalCalmTime;
    public float holdReformRadius;

    // Start delays: front row first, then startRowDelay per row plus a deterministic jitter.
    public float startRowDelay;
    public float startJitterDelay;

    // Formation destination updates (UnitNavMeshSystem)
    public float destinationChangeMin;
    public float destinationChangeSpeedFactor;
    public float destinationLeadTime;
    public float destinationLeadMaxDistance;
    public float slotVelocitySmoothing;

    // Constants moved out of system code with their current values
    public float combatFacingRange;
    public float formedRotationSpeed;
    public float runSpeedMultiplier;
    public float formationChangeCooldown;
    public bool avoidanceEnabled; // Allies dodge each other by velocity (C14).
    public float avoidanceNeighborRadius; // Allies closer than this are avoided (m, C14).
    public float avoidanceMaxNeighbors; // Nearest allies considered per unit.
    public float avoidanceTimeHorizon; // Collisions further away in time are ignored (s).
    public float avoidanceTimeHorizonNear; // Time horizon within one spacing of the slot (s).
    public float avoidanceCollisionResolveTime; // Time to separate overlapping allies (s).
    public float runEnterDistance; // Switch to Run beyond this distance to the slot (m).
    public float runExitDistance; // Back to Walk below this distance to the slot (m).
    public float followRecoveryTime; // Seconds to close the gap to the slot.
    public float followMinSpeedRatio; // Minimum following speed as a fraction of the base speed.
    public float bodyRadius; // Body radius for contact and avoidance, m (D10).
    public float meleeStopReachFraction; // Melee stop: fraction of the weapon reach past the body (C5).
    public float rangedStopFraction; // Ranged stop as a fraction of range (C5).
    public float combatTurnRate; // Max combat turn speed, degrees per second (C7).

    /// <summary>Values used when no tuning asset is assigned (and by tests).</summary>
    public static MovementTuningComponent Default => new MovementTuningComponent
    {
        arrivalRadiusIn = 0.2f,
        arrivalRadiusOut = 0.5f,
        arrivalCalmTime = 0.15f,
        holdReformRadius = 1f,
        startRowDelay = 0.08f,
        startJitterDelay = 0.1f,
        destinationChangeMin = 0.25f,
        destinationChangeSpeedFactor = 0.25f,
        destinationLeadTime = 0.4f,
        destinationLeadMaxDistance = 1f,
        slotVelocitySmoothing = 0.15f,
        combatFacingRange = 3.5f,
        formedRotationSpeed = 5f,
        runSpeedMultiplier = 2f,
        formationChangeCooldown = 1f,
        avoidanceEnabled = true,
        avoidanceNeighborRadius = 4f,
        avoidanceMaxNeighbors = 8f,
        avoidanceTimeHorizon = 2f,
        avoidanceTimeHorizonNear = 1f,
        avoidanceCollisionResolveTime = 0.25f,
        runEnterDistance = 0.9f,
        runExitDistance = 0.45f,
        followRecoveryTime = 0.5f,
        followMinSpeedRatio = 0.3f,
        bodyRadius = 0.5f,
        meleeStopReachFraction = 0.6f,
        rangedStopFraction = 0.92f,
        combatTurnRate = 450f,
    };

    /// <summary>
    /// Coherence rules of Movimiento_Formaciones.md §3 that apply to these fields.
    /// <paramref name="cellSize"/> is the formation grid spacing. Returns null when coherent.
    /// </summary>
    public static string Validate(in MovementTuningComponent t, float cellSize)
    {
        if (t.arrivalRadiusIn <= 0f) return "arrivalRadiusIn must be positive.";
        if (t.arrivalRadiusIn >= t.arrivalRadiusOut) return "arrivalRadiusIn must be smaller than arrivalRadiusOut.";
        if (t.arrivalRadiusOut > cellSize * 0.5f) return $"arrivalRadiusOut must not exceed half a grid cell ({cellSize * 0.5f}).";
        if (t.arrivalRadiusIn >= cellSize * 0.25f) return $"arrivalRadiusIn must be smaller than a quarter grid cell ({cellSize * 0.25f}).";
        if (t.holdReformRadius < t.arrivalRadiusIn) return "holdReformRadius must not be smaller than arrivalRadiusIn.";
        if (t.startRowDelay < 0f || t.startJitterDelay < 0f) return "Start delays must not be negative.";
        if (t.destinationLeadTime < 0f || t.destinationLeadMaxDistance < 0f) return "Destination lead must not be negative.";
        if (t.meleeStopReachFraction <= 0f || t.meleeStopReachFraction >= 1f) return "meleeStopReachFraction must be between 0 and 1.";
        if (t.rangedStopFraction <= 0f || t.rangedStopFraction >= 1f) return "rangedStopFraction must be between 0 and 1.";
        if (t.bodyRadius <= 0f) return "bodyRadius must be positive.";
        if (t.runExitDistance >= t.runEnterDistance) return "runExitDistance must be smaller than runEnterDistance.";
        if (t.followRecoveryTime <= 0f) return "followRecoveryTime must be positive.";
        if (t.avoidanceTimeHorizonNear > t.avoidanceTimeHorizon) return "avoidanceTimeHorizonNear must not exceed avoidanceTimeHorizon.";
        if (t.avoidanceMaxNeighbors > 28f) return "avoidanceMaxNeighbors must not exceed 28.";
        if (t.runExitDistance <= t.arrivalRadiusIn) return "runExitDistance must exceed arrivalRadiusIn.";
        if (t.followMinSpeedRatio < 0f || t.followMinSpeedRatio > 1f) return "followMinSpeedRatio must be between 0 and 1.";
        if (t.destinationChangeMin <= 0f) return "destinationChangeMin must be positive.";
        // The command must be re-sent before the agent reaches it, or the lead stops paying off.
        if (t.destinationLeadTime > 0f && t.destinationChangeSpeedFactor >= t.destinationLeadTime)
            return "destinationChangeSpeedFactor must be smaller than destinationLeadTime.";
        return null;
    }
}
