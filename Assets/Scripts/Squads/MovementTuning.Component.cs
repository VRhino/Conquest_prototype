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
    public float hurrySpeedMultiplier;
    public float formationChangeCooldown;
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
        hurrySpeedMultiplier = 2f,
        formationChangeCooldown = 1f,
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
        if (t.destinationChangeMin <= 0f) return "destinationChangeMin must be positive.";
        // The command must be re-sent before the agent reaches it, or the lead stops paying off.
        if (t.destinationLeadTime > 0f && t.destinationChangeSpeedFactor >= t.destinationLeadTime)
            return "destinationChangeSpeedFactor must be smaller than destinationLeadTime.";
        return null;
    }
}
